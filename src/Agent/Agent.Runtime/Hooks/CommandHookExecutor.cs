// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Framework.Hooks;
using Agent.Logging;
using Agent.Plugins.Helpers;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Hooks;

/// <summary>
/// Executes command-based hooks by running shell commands in the code execution sandbox.
/// The command receives hook context via stdin and outputs JSON with the hook result.
/// Supports both inline commands and multi-line scripts.
/// When the context contains a large execution summary, it is uploaded as a file
/// and the path is included in the context instead of the content.
/// </summary>
public class CommandHookExecutor : IHookExecutor
{
    private readonly ISessionPoolService _sessionPoolService;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<CommandHookExecutor> _logger;

    /// <summary>
    /// Cache of uploaded scripts, keyed by "{sessionId}:{scriptHash}".
    /// Value is the filename uploaded to /mnt/data.
    /// </summary>
    private static readonly ConcurrentDictionary<string, string> s_uploadedScripts = new();

    private static readonly JsonSerializerOptions s_serializeOptions = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private static readonly JsonSerializerOptions s_deserializeOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HookType SupportedType => HookType.Command;

    public CommandHookExecutor(
        ISessionPoolService sessionPoolService,
        IHostEnvironment hostEnvironment,
        ILogger<CommandHookExecutor> logger)
    {
        _sessionPoolService = sessionPoolService ?? throw new ArgumentNullException(nameof(sessionPoolService));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<HookResult> ExecuteAsync(HookDefinition hook, HookContext context, CancellationToken cancellationToken = default)
    {
        bool hasCommand = !string.IsNullOrWhiteSpace(hook.Command);
        bool hasScript = !string.IsNullOrWhiteSpace(hook.Script);

        if (!hasCommand && !hasScript)
        {
            _logger.LogInternalWarning("Command hook has no command or script defined, allowing action");
            return HookResult.Success();
        }

        if (context.ThreadId == Guid.Empty)
        {
            _logger.LogInternalWarning("No thread ID available for command hook, allowing action");
            return HookResult.Error("No thread ID available for session");
        }

        try
        {
            var sessionIdentifier = BuildSessionIdentifier(context);

            // If execution summary exists, upload it to the sandbox and replace with path
            if (!string.IsNullOrEmpty(context.ExecutionSummary))
            {
                var transcriptPath = await UploadTranscriptAsync(sessionIdentifier, context);
                context.ExecutionSummary = transcriptPath;
                _logger.LogInternalDebug("Uploaded execution summary to {TranscriptPath}", transcriptPath);
            }

            var contextJson = JsonSerializer.Serialize(context, context.GetType(), s_serializeOptions);
            var escapedJson = EscapeForBash(contextJson);

            string fullCommand;
            if (hasScript)
            {
                // Upload the script and execute it
                var scriptFilename = await EnsureScriptUploadedAsync(
                    sessionIdentifier, hook.Script!, context.HookEventName, cancellationToken);
                fullCommand = $"cd /mnt/data && chmod +x {scriptFilename} && echo '{escapedJson}' | ./{scriptFilename}";
                _logger.LogInternalInformation("Executing script hook for {EventType}: {ScriptFile}", context.HookEventName, scriptFilename);
            }
            else
            {
                // Execute inline command
                fullCommand = $"echo '{escapedJson}' | {hook.Command}";
                _logger.LogInternalInformation("Executing command hook for {EventType}: {Command}", context.HookEventName, hook.Command);
            }

            // Execute the command in the session pool
            var response = await _sessionPoolService.ExecuteShellCommandInCodeInterpreterPoolAsync(
                fullCommand,
                sessionIdentifier,
                hook.Timeout);

            return HandleResponse(hook, response);
        }
        catch (OperationCanceledException)
        {
            var errorMessage = $"Command hook timed out after {hook.Timeout} seconds";
            _logger.LogInternalWarning(errorMessage);
            return HandleError(hook, errorMessage);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error executing command hook");
            return HandleError(hook, ex.Message);
        }
    }

    /// <summary>
    /// Uploads the execution summary/transcript to the sandbox.
    /// </summary>
    /// <param name="sessionId">The session identifier.</param>
    /// <param name="context">The hook context containing the execution summary.</param>
    /// <returns>The path to the transcript file in the sandbox (/mnt/data/...).</returns>
    private async Task<string> UploadTranscriptAsync(string sessionId, HookContext context)
    {
        var content = context.ExecutionSummary!;
        var hash = ComputeScriptHash(content);
        var filename = $"hook_transcript_{context.ThreadId}_{hash}.txt";

        _logger.LogInternalDebug("Uploading transcript to session: {Filename} ({Length} chars)", filename, content.Length);

        await _sessionPoolService.UploadSessionFileAsync(
            sessionId,
            filename,
            Encoding.UTF8.GetBytes(content));

        return $"/mnt/data/{filename}";
    }

    /// <summary>
    /// Ensures the script is uploaded to the session, using a cache to avoid re-uploading.
    /// Scripts are hashed to create unique filenames.
    /// </summary>
    private async Task<string> EnsureScriptUploadedAsync(
        string sessionId, string script, HookEventType eventType, CancellationToken ct)
    {
        var hash = ComputeScriptHash(script);
        var cacheKey = $"{sessionId}:{hash}";

        if (s_uploadedScripts.TryGetValue(cacheKey, out var filename))
        {
            // list remote files to make sure the file still exists (in case it was cleaned up)
            var remoteFilesJson = await _sessionPoolService.ListSessionFilesAsync(sessionId);

            var remoteFiles = JsonSerializer.Deserialize<FilesListResponse>(remoteFilesJson);
            if (remoteFiles != null && remoteFiles.Value?.Any(fiw => fiw.Properties?.Filename == filename) == true)
            {
                _logger.LogInternalInformation("Script already uploaded for session: {Filename}", filename);
                return filename;
            }
        }

        var extension = GetScriptExtension(script);
        filename = $"hook_{eventType}_{hash}{extension}";
        _logger.LogInternalInformation("Uploading script to session: {Filename}", filename);

        await _sessionPoolService.UploadSessionFileAsync(
            sessionId, filename, Encoding.UTF8.GetBytes(script));

        s_uploadedScripts.TryAdd(cacheKey, filename);
        return filename;
    }

    /// <summary>
    /// Determines the file extension based on the script's shebang line.
    /// Returns ".py" for Python scripts, ".sh" for bash or as default.
    /// </summary>
    private static string GetScriptExtension(string script)
    {
        var firstLine = script.Split('\n', 2)[0].Trim();

        if (firstLine.StartsWith(Core.Constants.Hooks.PythonShebang))
        {
            return ".py";
        }

        // Default to .sh for bash scripts or if no shebang specified
        return ".sh";
    }

    /// <summary>
    /// Computes a short hash of the script content for caching and filename uniqueness.
    /// </summary>
    private static string ComputeScriptHash(string content)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(content));
        return Convert.ToHexString(bytes)[..12].ToLowerInvariant();
    }

    /// <summary>
    /// Handles the response from the command execution using Claude-style exit code semantics.
    /// Exit 0: Parse stdout as JSON
    /// Exit 2: Always blocking error (ignore failMode)
    /// Other: Use failMode to determine blocking behavior
    /// </summary>
    private HookResult HandleResponse(HookDefinition hook, Common.ApiModels.Session.SessionResponse response)
    {
        var exitCode = response.ExitCode ?? -1;
        var stdout = response.Result?.Stdout ?? string.Empty;
        var stderr = response.Result?.Stderr ?? string.Empty;

        _logger.LogInternalInformation("Command hook completed. ExitCode={ExitCode}, Stdout={Stdout}, Stderr={Stderr}",
            exitCode, stdout, stderr);

        if (exitCode == 0)
        {
            // Exit 0: Parse stdout as JSON
            return ParseResponse(hook, stdout);
        }

        if (exitCode == 2)
        {
            // Exit 2: Always blocking, ignore failMode
            var msg = !string.IsNullOrWhiteSpace(stderr) ? stderr.Trim() : "Hook exited with code 2";
            _logger.LogInternalWarning("Command hook returned blocking exit code 2: {Message}", msg);
            return HookResult.Reject(msg);
        }

        // Other exit codes: use failMode
        var errorMessage = $"Command exited with code {exitCode}: {stderr}";
        _logger.LogInternalWarning("Command hook failed: {Error}", errorMessage);
        return HandleError(hook, errorMessage);
    }

    /// <summary>
    /// Builds a stable session identifier for hook command execution.
    /// Uses agent name from environment and thread ID from context.
    /// </summary>
    private string BuildSessionIdentifier(HookContext context)
    {
        var agentName = AgentNameHelper.GetAgentName(!_hostEnvironment.IsDevelopment());
        var threadId = context.ThreadId.ToString();
        return _sessionPoolService.BuildSessionIdentifier(agentName, threadId, false);
    }

    private HookResult HandleError(HookDefinition hook, string error)
    {
        return hook.FailMode == HookFailMode.Block
            ? HookResult.ErrorBlocking(error)
            : HookResult.Error(error);
    }

    private HookResult ParseResponse(HookDefinition hook, string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            // Empty output is treated as success (no objection)
            _logger.LogInternalInformation("Command hook produced no output, treating as success");
            return HookResult.Success();
        }

        try
        {
            var jsonText = ExtractJson(stdout);
            var result = JsonSerializer.Deserialize<HookResult>(jsonText, s_deserializeOptions);

            if (result != null)
            {
                // If decision is "block" but no reason provided, use a default
                if (!result.Ok && string.IsNullOrEmpty(result.Reason))
                {
                    result.Reason = "Blocked by hook";
                }

                _logger.LogInternalInformation(
                    "Command hook result: Ok={Ok}, Decision={Decision}, Reason={Reason}, HasContext={HasContext}",
                    result.Ok, result.Decision, result.Reason, result.GetAdditionalContext() != null);

                return result;
            }
        }
        catch (JsonException ex)
        {
            _logger.LogInternalWarning(ex, "Failed to parse command hook output as JSON: {Output}", stdout);
        }

        // If parsing fails, handle according to fail mode
        _logger.LogInternalWarning("Could not parse command hook response, handling according to failMode");
        return HandleError(hook, $"Failed to parse hook output: {stdout}");
    }

    private static string ExtractJson(string text)
    {
        var trimmed = text.Trim();

        // Remove markdown code block markers if present
        if (trimmed.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            trimmed = trimmed[7..];
        }
        else if (trimmed.StartsWith("```"))
        {
            trimmed = trimmed[3..];
        }

        if (trimmed.EndsWith("```"))
        {
            trimmed = trimmed[..^3];
        }

        return trimmed.Trim();
    }

    private static string EscapeForBash(string input)
    {
        // Escape single quotes for bash: replace ' with '\''
        return input.Replace("'", "'\\''");
    }
}
