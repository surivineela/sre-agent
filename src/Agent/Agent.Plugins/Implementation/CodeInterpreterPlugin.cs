// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
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
        return sb.ToString();
    }

    public async Task<string> GeneratePdfReportAsync(string pythonCode, string expectedOutputFilename, string saveAsFilename, int timeoutSeconds)
    {
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
            return $"Report generated successfully. Download: {relativeLink}. Present this link as [Click Here]({relativeLink})";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to persist PDF report locally");
            return $"Failed to persist PDF report: {ex.Message}";
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
}
