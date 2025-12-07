// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Session;
using Agent.Core.Services;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins;

/// <summary>
/// Executes limited Python code via the dedicated ACA Sessions "code interpreter" pool.
/// Enforces a static allow/deny policy to avoid outbound network / privileged operations.
/// </summary>
public class CodeInterpreterPlugin : ICodeInterpreterPlugin
{
    private readonly ILogger<CodeInterpreterPlugin> _logger;
    private readonly ISessionPoolService _sessionPoolService;
    private readonly IHostEnvironment _hostEnvironment;

    public Guid? ThreadId { get; set; }

    public CodeInterpreterPlugin(
        ILogger<CodeInterpreterPlugin> logger,
        ISessionPoolService sessionPoolService,
        IHostEnvironment hostEnvironment)
    {
        _logger = logger;
        _sessionPoolService = sessionPoolService;
        _hostEnvironment = hostEnvironment;
    }

    public async Task<string> ExecutePythonCodeAsync(string pythonCode, int timeoutSeconds)
    {
        var validation = ValidatePython(pythonCode);
        if (validation != null)
        {
            return validation;
        }

        var identifier = BuildIdentifier();
        var execResp = await _sessionPoolService.ExecutePythonInlineAsync(pythonCode, identifier, timeoutSeconds);

        var sb = new StringBuilder();
        sb.AppendLine($"ExitCode: {execResp.ExitCode?.ToString() ?? "(n/a)"}");
        if (!string.IsNullOrWhiteSpace(execResp.Stdout))
        {
            sb.AppendLine("STDOUT:");
            sb.AppendLine(Truncate(execResp.Stdout, 4000));
        }
        if (!string.IsNullOrWhiteSpace(execResp.Stderr))
        {
            sb.AppendLine("STDERR:");
            sb.AppendLine(Truncate(execResp.Stderr, 2000));
        }

        // Automatically list and retrieve all generated files from the session
        try
        {
            var filesJson = await _sessionPoolService.ListSessionFilesAsync(identifier);

            // Parse the files list using proper JSON deserialization
            if (!string.IsNullOrWhiteSpace(filesJson))
            {
                var filesResponse = JsonSerializer.Deserialize<FilesListResponse>(filesJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                if (filesResponse?.Value != null && filesResponse.Value.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("=== AUTO-RETRIEVED FILES ===");

                    foreach (var fileWrapper in filesResponse.Value)
                    {
                        if (fileWrapper.Properties == null)
                        {
                            continue;
                        }

                        var file = fileWrapper.Properties;

                        // Skip if filename is empty or if it's a directory indicator
                        if (string.IsNullOrWhiteSpace(file.Filename) || file.Filename.EndsWith('/'))
                        {
                            continue;
                        }

                        try
                        {
                            var fileBytes = await _sessionPoolService.DownloadSessionFileAsync(identifier, file.Filename);

                            var reportsDir = Path.Combine(AppContext.BaseDirectory, "reports");
                            Directory.CreateDirectory(reportsDir);
                            var targetPath = Path.Combine(reportsDir, Path.GetFileName(file.Filename));
                            await File.WriteAllBytesAsync(targetPath, fileBytes);

                            var extension = Path.GetExtension(file.Filename).ToLowerInvariant();
                            var relativeLink = $"/api/files/{Uri.EscapeDataString(Path.GetFileName(file.Filename))}";

                            // Check if it's an image file (matplotlib, seaborn, PIL outputs)
                            if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" or
                                ".bmp" or ".tiff" or ".tif" or ".ico" or ".eps" or ".ps")
                            {
                                sb.AppendLine($"📊 Image: ![{Path.GetFileName(file.Filename)}]({relativeLink})");
                            }
                            // Data files and spreadsheets
                            else if (extension is ".csv" or ".tsv" or ".xlsx" or ".xls" or ".xlsm" or ".ods" or
                                     ".json" or ".xml" or ".yaml" or ".yml")
                            {
                                sb.AppendLine($"📊 Data: [Download {Path.GetFileName(file.Filename)}]({relativeLink})");
                            }
                            // Documents and reports
                            else if (extension is ".pdf" or ".html" or ".htm" or ".md" or ".docx" or ".doc" or
                                     ".pptx" or ".ppt" or ".txt" or ".rtf")
                            {
                                sb.AppendLine($"📄 Document: [Download {Path.GetFileName(file.Filename)}]({relativeLink})");
                            }
                            // Code and notebooks
                            else if (extension is ".py" or ".ipynb" or ".r" or ".sql" or ".sh")
                            {
                                sb.AppendLine($"� Code: [Download {Path.GetFileName(file.Filename)}]({relativeLink})");
                            }
                            // Archives and scientific data
                            else if (extension is ".zip" or ".tar" or ".gz" or ".h5" or ".hdf5" or ".nc" or
                                     ".mat" or ".npz" or ".pkl" or ".pickle")
                            {
                                sb.AppendLine($"🗜️ Archive/Data: [Download {Path.GetFileName(file.Filename)}]({relativeLink})");
                            }
                            else
                            {
                                sb.AppendLine($"📁 File: [Download {Path.GetFileName(file.Filename)}]({relativeLink})");
                            }

                            sb.AppendLine("To make these files available to the user, they must be presented in markdown syntax. eg: [FileName.txt](/api/files/FileName.txt). Else users would not be able to download these files");
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInternalWarning($"Failed to auto-retrieve file '{file.Filename}': {ex.Message}");
                            // Don't fail the whole operation if one file fails
                        }
                    }

                    sb.AppendLine("=== END AUTO-RETRIEVED FILES ===");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to auto-retrieve session files");
            // Don't fail the whole operation if file retrieval fails
        }

        return sb.ToString();
    }

    public async Task<string> ExecuteShellCommandAsync(string command, string explanation, bool isBackground, int timeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            return "Shell command cannot be empty.";
        }

        if (isBackground)
        {
            return "Background execution is not supported in the code interpreter sandbox.";
        }

        timeoutSeconds = Math.Clamp(timeoutSeconds, 5, 240);

        var trimmedCommand = command.Trim();
        if (trimmedCommand.StartsWith("/", StringComparison.Ordinal))
        {
            return "Absolute paths are not permitted; stay within /mnt/data using relative paths.";
        }

        if (trimmedCommand.Contains("../", StringComparison.Ordinal) || trimmedCommand.Contains("..\\", StringComparison.Ordinal))
        {
            return "Path traversal ('../') is not allowed. Keep shell operations within /mnt/data.";
        }

        if (!string.IsNullOrWhiteSpace(explanation))
        {
            _logger.LogInternalInformation($"Code interpreter shell command explanation: {explanation}");
        }

        var identifier = BuildIdentifier();
        var finalCommand = $"cd /mnt/data && {trimmedCommand}";

        SessionResponse response;
        try
        {
            response = await _sessionPoolService.ExecuteShellCommandInCodeInterpreterPoolAsync(finalCommand, identifier, timeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed executing shell command in code interpreter pool");
            return $"Failed to execute shell command: {ex.Message}";
        }

        return FormatSessionResponse(response, false);
    }

    public async Task<string> GeneratePdfReportAsync(string pythonCode, string expectedOutputFilename, string saveAsFilename, int timeoutSeconds)
    {
        // NOTE: This method MUST return a markdown link in the format [Link Text](/api/files/filename)
        // for the agent to properly present download links to the user
        if (!expectedOutputFilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return "expectedOutputFilename must end with .pdf";
        if (!saveAsFilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return "saveAsFilename must end with .pdf";
        var validation = ValidatePython(pythonCode);
        if (validation != null) return validation;
        var identifier = BuildIdentifier();

        // Execute code (which should write the PDF into /mnt/data/<expectedOutputFilename>, using a relative path)
        await _sessionPoolService.ExecutePythonInlineAsync(pythonCode, identifier, timeoutSeconds);

        // Download file bytes from session pool
        var sanitizedExpected = expectedOutputFilename.Replace("..", string.Empty).TrimStart('/');
        byte[] fileBytes;
        try
        {
            fileBytes = await _sessionPoolService.DownloadSessionFileAsync(identifier, sanitizedExpected);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to download PDF from session");
            return $"Failed to retrieve generated PDF: {ex.Message}";
        }

        try
        {
            var reportsDir = Path.Combine(AppContext.BaseDirectory, "reports");
            Directory.CreateDirectory(reportsDir);
            var targetPath = Path.Combine(reportsDir, Path.GetFileName(saveAsFilename));
            await File.WriteAllBytesAsync(targetPath, fileBytes);
            var relativeLink = $"/api/files/{Uri.EscapeDataString(Path.GetFileName(saveAsFilename))}"; // prefer /api/files (reports route still supported)
            return $"✅ PDF report generated successfully. Download: [Download {saveAsFilename}]({relativeLink})";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to persist PDF report locally");
            return $"Failed to persist PDF report: {ex.Message}";
        }
    }

    public async Task<string> ReadSessionFileAsync(string relativePath, int offset, int limit)
    {
        if (string.IsNullOrWhiteSpace(relativePath))
        {
            return "filePath cannot be empty.";
        }

        string sanitizedPath;
        try
        {
            sanitizedPath = SanitizeRelativePath(relativePath);
        }
        catch (Exception ex)
        {
            return $"Invalid file path '{relativePath}': {ex.Message}";
        }

        if (string.IsNullOrWhiteSpace(sanitizedPath))
        {
            return "A specific file path within /mnt/data must be provided.";
        }

        offset = Math.Max(1, offset);
        limit = Math.Clamp(limit <= 0 ? 200 : limit, 1, 2000);

        var identifier = BuildIdentifier();
        byte[] fileBytes;
        try
        {
            fileBytes = await _sessionPoolService.DownloadSessionFileAsync(identifier, $"mnt/data/{sanitizedPath}");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to download session file '{sanitizedPath}'");
            return $"Failed to read file '{relativePath}': {ex.Message}";
        }

        if (fileBytes.Length == 0)
        {
            return $"File '{relativePath}' is empty.";
        }

        if (LooksBinary(fileBytes))
        {
            return $"File '{relativePath}' appears to be binary; text preview is not supported.";
        }

        var content = Encoding.UTF8.GetString(fileBytes);
        var normalized = NormalizeLineEndings(content);
        var lines = normalized.Split('\n');
        var totalLines = lines.Length;

        if (totalLines > 0 && lines[totalLines - 1].Length == 0)
        {
            totalLines -= 1;
        }

        if (totalLines == 0)
        {
            return $"File '{relativePath}' is empty.";
        }

        var startIndex = offset - 1;
        if (startIndex >= totalLines)
        {
            return $"File '{relativePath}' has {totalLines} line(s); requested offset {offset} is beyond the end.";
        }

        var endIndex = Math.Min(startIndex + limit, totalLines);

        var sb = new StringBuilder();
        sb.AppendLine($"Showing lines {startIndex + 1}-{endIndex} of {totalLines} from /mnt/data/{sanitizedPath}:");
        sb.AppendLine("```");
        for (var i = startIndex; i < endIndex; i++)
        {
            var line = lines[i];
            sb.AppendLine($"{i + 1}: {line}");
        }
        sb.AppendLine("```");
        if (endIndex < totalLines)
        {
            sb.AppendLine($"... {totalLines - endIndex} additional line(s) not shown. Increase limit to view more.");
        }

        return sb.ToString();
    }

    public async Task<string> ListSessionFilesAsync()
    {
        var identifier = BuildIdentifier();
        try
        {
            var filesJson = await _sessionPoolService.ListSessionFilesAsync(identifier);
            return $"Files in session /mnt/data directory:\n{filesJson}";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to list session files");
            return $"Failed to list session files: {ex.Message}";
        }
    }

    public async Task<string> GetSessionFileAsync(string filename, string saveAsFilename)
    {
        // NOTE: This method MUST return markdown links in the format [Link Text](/api/files/filename)
        // for the agent to properly present download links to the user
        var sanitizedFilename = filename.Replace("..", string.Empty).TrimStart('/');
        var identifier = BuildIdentifier();

        byte[] fileBytes;
        try
        {
            fileBytes = await _sessionPoolService.DownloadSessionFileAsync(identifier, sanitizedFilename);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to download file from session");
            return $"Failed to retrieve file '{filename}': {ex.Message}";
        }

        try
        {
            var reportsDir = Path.Combine(AppContext.BaseDirectory, "reports");
            Directory.CreateDirectory(reportsDir);
            var targetPath = Path.Combine(reportsDir, Path.GetFileName(saveAsFilename));
            await File.WriteAllBytesAsync(targetPath, fileBytes);

            var extension = Path.GetExtension(saveAsFilename).ToLowerInvariant();
            var relativeLink = $"/api/files/{Uri.EscapeDataString(Path.GetFileName(saveAsFilename))}";

            // Check if it's an image file (matplotlib, seaborn, PIL, and other graphics outputs)
            if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" or
                ".bmp" or ".tiff" or ".tif" or ".ico")
            {
                return $"Image file retrieved successfully. To display the image inline, use: ![{saveAsFilename}]({relativeLink})\n\nDirect download link: [Download {saveAsFilename}]({relativeLink})";
            }

            return $"File retrieved successfully. Download: [Download {saveAsFilename}]({relativeLink})";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to persist file locally");
            return $"Failed to persist file: {ex.Message}";
        }
    }

    public async Task<string> GrepSessionFilesAsync(string query, bool isRegexp, string includePattern, int maxResults, int timeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return "query cannot be empty.";
        }

        timeoutSeconds = Math.Clamp(timeoutSeconds, 5, 900);
        maxResults = Math.Clamp(maxResults <= 0 ? 50 : maxResults, 1, 500);

        if (!string.IsNullOrWhiteSpace(includePattern))
        {
            try
            {
                ValidateIncludePattern(includePattern);
            }
            catch (Exception ex)
            {
                return $"Invalid includePattern '{includePattern}': {ex.Message}";
            }
        }

        var identifier = BuildIdentifier();
        var flags = isRegexp ? "-RInH" : "-RIFnH";
        var includeClause = string.IsNullOrWhiteSpace(includePattern)
            ? string.Empty
            : $" --include={QuoteForBash(includePattern)}";

        var commandBuilder = new StringBuilder();
        commandBuilder.Append("set -o pipefail && cd /mnt/data && ");
        commandBuilder.Append($"grep {flags}{includeClause} -- {QuoteForBash(query)}");
        commandBuilder.Append($" | head -n {maxResults}");

        SessionResponse response;
        try
        {
            response = await _sessionPoolService.ExecuteShellCommandInCodeInterpreterPoolAsync(commandBuilder.ToString(), identifier, timeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed executing grep search in code interpreter pool");
            return $"Failed to search files: {ex.Message}";
        }

        var exitCode = response.ExitCode ?? -1;
        var stdout = response.Result?.Stdout ?? string.Empty;
        var stderr = response.Result?.Stderr ?? string.Empty;

        if (exitCode == 1 || string.IsNullOrWhiteSpace(stdout))
        {
            return "No matches found.";
        }

        if (exitCode == 141)
        {
            stderr = string.Empty; // Broken pipe when head stops early; safe to ignore.
        }
        else if (exitCode != 0)
        {
            var err = string.IsNullOrWhiteSpace(stderr) ? "Unknown error." : stderr;
            return $"Search failed (exit code {exitCode}). Details: {Truncate(err, 500)}";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Search results (up to {maxResults} matches):");
        sb.AppendLine("```");
        sb.AppendLine(Truncate(stdout.TrimEnd(), 4000));
        sb.AppendLine("```");
        if (!string.IsNullOrWhiteSpace(stderr))
        {
            sb.AppendLine("Warnings:");
            sb.AppendLine(Truncate(stderr.Trim(), 500));
        }

        return sb.ToString();
    }

    private string? ValidatePython(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "pythonCode cannot be empty";
        if (code.Length > 20_000) return "pythonCode too large (limit 20k chars)";
        // Runtime safety enforcement (network / subprocess / installs) is handled by ACA Sessions sandbox; prompt instructions reinforce policy.
        return null;
    }

    private static string SanitizeRelativePath(string rawPath)
    {
        var normalized = rawPath.Replace('\\', '/').Trim();

        while (normalized.StartsWith("./", StringComparison.Ordinal))
        {
            normalized = normalized[2..];
        }

        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            normalized = normalized.TrimStart('/');
        }

        const string dataPrefix = "mnt/data/";
        if (normalized.StartsWith(dataPrefix, StringComparison.Ordinal))
        {
            normalized = normalized[dataPrefix.Length..];
        }

        if (normalized.Length == 0)
        {
            return normalized;
        }

        var segments = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        foreach (var segment in segments)
        {
            if (segment == "." || segment == "..")
            {
                throw new InvalidOperationException("Path traversal is not permitted. Provide a relative path within /mnt/data.");
            }
        }

        return string.Join('/', segments);
    }

    private static void ValidateIncludePattern(string pattern)
    {
        var normalized = pattern.Replace('\\', '/').Trim();
        if (normalized.StartsWith("/", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Patterns must be relative (no leading '/').");
        }

        if (normalized.Contains("../", StringComparison.Ordinal))
        {
            throw new InvalidOperationException("Patterns cannot traverse directories (no '../').");
        }
    }

    private static string NormalizeLineEndings(string value)
        => value.Replace("\r\n", "\n").Replace('\r', '\n');

    private static bool LooksBinary(IReadOnlyList<byte> bytes)
    {
        var sample = Math.Min(bytes.Count, 1024);
        for (var i = 0; i < sample; i++)
        {
            if (bytes[i] == 0)
            {
                return true;
            }
        }

        return false;
    }

    private static string QuoteForBash(string value)
        => "'" + value.Replace("'", "'\"'\"'") + "'";

    /// <summary>
    /// Builds a stable session identifier for the code interpreter pool using the agent name and thread id.
    /// This ensures all python/report generation code for the same (agent, thread) reuses the same underlying session when supported by the pool.
    /// </summary>
    private string BuildIdentifier()
    {
        var agentName = AgentNameHelper.GetAgentName(!_hostEnvironment.IsDevelopment());
        var threadId = ThreadId?.ToString() ?? Guid.NewGuid().ToString();
        return _sessionPoolService.BuildSessionIdentifier(agentName, threadId, false);
    }

    // Legacy shell path retained for backward compatibility (unused after inline API migration)
    private async Task<SessionResponse> ExecuteInInterpreterPoolAsync(string command, string identifier, int timeoutSeconds)
        => await _sessionPoolService.ExecuteShellCommandInCodeInterpreterPoolAsync(command, identifier, timeoutSeconds);

    // Base64 extraction no longer used (kept for potential backward compatibility if needed in future revisions)
    private static string? ExtractPdf(string stdout) => null;

    private static string FormatSessionResponse(SessionResponse response, bool includeArtifacts)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"ExitCode: {response.ExitCode}");
        if (!string.IsNullOrWhiteSpace(response.Result?.Stdout))
        {
            sb.AppendLine("STDOUT:");
            sb.AppendLine(Truncate(response.Result.Stdout, 4000));
        }
        if (!string.IsNullOrWhiteSpace(response.Result?.Stderr))
        {
            sb.AppendLine("STDERR:");
            sb.AppendLine(Truncate(response.Result.Stderr, 2000));
        }
        return sb.ToString();
    }

    private static string Truncate(string value, int max)
        => value.Length <= max ? value : value.Substring(0, max) + "...<truncated>";

    // Internal classes for JSON deserialization
    private class FilesListResponse
    {
        public List<FileItemWrapper>? Value { get; set; }
    }

    private class FileItemWrapper
    {
        public FileMetadata? Properties { get; set; }
    }

    private class FileMetadata
    {
        public string Filename { get; set; } = string.Empty;
        public long? Size { get; set; }
        public DateTime? LastModifiedTime { get; set; }
    }
}
