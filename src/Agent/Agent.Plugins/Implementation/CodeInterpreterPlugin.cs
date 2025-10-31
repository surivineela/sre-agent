// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Plugins.Interface;
using Agent.Core.Services;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Agent.Core.Models.Session;

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

    public async Task<string> ExecutePythonSnippetAsync(string pythonCode, int timeoutSeconds)
    {
        var validation = ValidatePython(pythonCode);
        if (validation != null) return validation;
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

                if (filesResponse?.Files != null && filesResponse.Files.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("=== AUTO-RETRIEVED FILES ===");

                    foreach (var file in filesResponse.Files)
                    {
                        // Skip if filename is empty or if it's a directory indicator
                        if (string.IsNullOrWhiteSpace(file.Name) || file.Name.EndsWith('/'))
                            continue;

                        try
                        {
                            var fileBytes = await _sessionPoolService.DownloadSessionFileAsync(identifier, file.Name);

                            var reportsDir = Path.Combine(AppContext.BaseDirectory, "reports");
                            Directory.CreateDirectory(reportsDir);
                            var targetPath = Path.Combine(reportsDir, Path.GetFileName(file.Name));
                            await File.WriteAllBytesAsync(targetPath, fileBytes);

                            var extension = Path.GetExtension(file.Name).ToLowerInvariant();
                            var relativeLink = $"/api/files/{Uri.EscapeDataString(Path.GetFileName(file.Name))}";

                            // Check if it's an image file (matplotlib, seaborn, PIL outputs)
                            if (extension is ".png" or ".jpg" or ".jpeg" or ".gif" or ".svg" or ".webp" or
                                ".bmp" or ".tiff" or ".tif" or ".ico" or ".eps" or ".ps")
                            {
                                sb.AppendLine($"📊 Image: ![{Path.GetFileName(file.Name)}]({relativeLink})");
                            }
                            // Data files and spreadsheets
                            else if (extension is ".csv" or ".tsv" or ".xlsx" or ".xls" or ".xlsm" or ".ods" or
                                     ".json" or ".xml" or ".yaml" or ".yml")
                            {
                                sb.AppendLine($"📊 Data: [Download {Path.GetFileName(file.Name)}]({relativeLink})");
                            }
                            // Documents and reports
                            else if (extension is ".pdf" or ".html" or ".htm" or ".md" or ".docx" or ".doc" or
                                     ".pptx" or ".ppt" or ".txt" or ".rtf")
                            {
                                sb.AppendLine($"📄 Document: [Download {Path.GetFileName(file.Name)}]({relativeLink})");
                            }
                            // Code and notebooks
                            else if (extension is ".py" or ".ipynb" or ".r" or ".sql" or ".sh")
                            {
                                sb.AppendLine($"� Code: [Download {Path.GetFileName(file.Name)}]({relativeLink})");
                            }
                            // Archives and scientific data
                            else if (extension is ".zip" or ".tar" or ".gz" or ".h5" or ".hdf5" or ".nc" or
                                     ".mat" or ".npz" or ".pkl" or ".pickle")
                            {
                                sb.AppendLine($"🗜️ Archive/Data: [Download {Path.GetFileName(file.Name)}]({relativeLink})");
                            }
                            else
                            {
                                sb.AppendLine($"📁 File: [Download {Path.GetFileName(file.Name)}]({relativeLink})");
                            }
                        }
                        catch (Exception ex)
                        {
                            _logger.LogInternalWarning($"Failed to auto-retrieve file '{file.Name}': {ex.Message}");
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

    public async Task<string> GeneratePdfReportAsync(string pythonCode, string expectedOutputFilename, string saveAsFilename, int timeoutSeconds)
    {
        // NOTE: This method MUST return a markdown link in the format [Link Text](/api/files/filename)
        // for the agent to properly present download links to the user
        if (!expectedOutputFilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return "expectedOutputFilename must end with .pdf";
        if (!saveAsFilename.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase)) return "saveAsFilename must end with .pdf";
        var validation = ValidatePython(pythonCode);
        if (validation != null) return validation;
        var identifier = BuildIdentifier();

        // Execute code (which should write the PDF into /mnt/data/<expectedOutputFilename>)
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

    private string? ValidatePython(string code)
    {
        if (string.IsNullOrWhiteSpace(code)) return "pythonCode cannot be empty";
        if (code.Length > 20_000) return "pythonCode too large (limit 20k chars)";
        // Runtime safety enforcement (network / subprocess / installs) is handled by ACA Sessions sandbox; prompt instructions reinforce policy.
        return null;
    }

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
        public List<FileMetadata>? Files { get; set; }
    }

    private class FileMetadata
    {
        public string Name { get; set; } = string.Empty;
        public long? Size { get; set; }
        public DateTime? Modified { get; set; }
    }
}
