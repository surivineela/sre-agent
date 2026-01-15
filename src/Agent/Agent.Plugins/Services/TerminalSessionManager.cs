// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Agent.Core.Services;
using Agent.Plugins.Implementation;
using Agent.Plugins.Models.WorkspaceTools;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Services;

/// <summary>
/// Manages terminal sessions with OSC 633 shell integration.
/// Provides one session per thread with auto-restart on process death.
/// </summary>
public class TerminalSessionManager : IDisposable
{
    private readonly ILogger<TerminalSessionManager> _logger;
    private readonly ConcurrentDictionary<Guid, TerminalSession> _sessions = new();
    private int _sessionCounter;
    private bool _disposing;
    private bool _disposed;

    public TerminalSessionManager(ILogger<TerminalSessionManager> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Gets or creates a terminal session for the current thread.
    /// </summary>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>The terminal session for this thread.</returns>
    public async Task<TerminalSession> GetOrCreateSessionAsync(CancellationToken ct = default)
    {
        var threadId = ThreadContextAccessor.CurrentThreadId
            ?? throw new InvalidOperationException("No thread context available");

        // Check for existing alive session
        if (_sessions.TryGetValue(threadId, out var existing) && existing.IsAlive)
        {
            return existing;
        }

        // Session died or doesn't exist - create new
        _logger.LogInternalInformation("Creating new terminal session for thread {ThreadId}", threadId);

        var session = await CreateSessionWithRetryAsync(ct);
        _sessions[threadId] = session;

        // Subscribe to process death for logging (auto-restart happens on next GetOrCreateSessionAsync)
        session.ProcessExited += (s, e) => OnProcessExited(threadId, session);

        return session;
    }

    private async Task<TerminalSession> CreateSessionWithRetryAsync(CancellationToken ct)
    {
        for (var attempt = 1; attempt <= TerminalConfiguration.MaxInitRetries; attempt++)
        {
            try
            {
                return await CreateSessionAsync(ct);
            }
            catch (TerminalInitializationException ex) when (attempt < TerminalConfiguration.MaxInitRetries)
            {
                _logger.LogInternalWarning(ex, "Session init failed, retrying ({Attempt}/{Max})",
                    attempt, TerminalConfiguration.MaxInitRetries);
            }
        }

        throw new TerminalInitializationException(
            $"Failed to initialize terminal after {TerminalConfiguration.MaxInitRetries} attempts");
    }

    private async Task<TerminalSession> CreateSessionAsync(CancellationToken ct)
    {
        var id = $"term-{Interlocked.Increment(ref _sessionCounter)}";
        var shell = GetShellPath();

        var psi = new ProcessStartInfo
        {
            FileName = shell,
            Arguments = "-i",  // Interactive mode required for PROMPT_COMMAND
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true,
            WorkingDirectory = WorkspaceToolsPlugin.SandboxRoot
        };

        var process = Process.Start(psi)
            ?? throw new TerminalInitializationException("Failed to start bash process");

        var session = new TerminalSession
        {
            Id = id,
            Process = process,
            CreatedAt = DateTime.UtcNow
        };

        // Start background reader (merges stdout + stderr)
        session.StartReader();

        // Inject OSC 633 shell integration
        // Use semicolons to run as single command to minimize intermediate 633;D signals
        var integration = """
            PROMPT_COMMAND='printf "\x1b]633;D;$?\x07"'; trap 'printf "\x1b]633;C\x07"' DEBUG
            """;
        await session.WriteLineAsync(integration);

        // Brief delay to let integration setup complete and drain any initial 633;D signals
        await Task.Delay(TimeSpan.FromSeconds(1));

        // Wait for integration to be ready (probe command)
        //await session.VerifyIntegrationAsync(TerminalConfiguration.InitTimeout, ct);

        _logger.LogInternalInformation("Created terminal session {SessionId} with PID {ProcessId}",
            id, process.Id);

        return session;
    }

    private void OnProcessExited(Guid threadId, TerminalSession session)
    {
        if (_disposing)
        {
            return;
        }

        _logger.LogInternalWarning("Terminal {SessionId} process exited unexpectedly, will recreate on next use",
            session.Id);

        // Remove dead session so next call creates a new one
        _sessions.TryRemove(threadId, out _);
    }

    /// <summary>
    /// Gets the current session for this thread, if any.
    /// </summary>
    public TerminalSession? GetCurrentSession()
    {
        var threadId = ThreadContextAccessor.CurrentThreadId;
        if (threadId == null)
        {
            return null;
        }

        if (_sessions.TryGetValue(threadId.Value, out var session) && session.IsAlive)
        {
            return session;
        }

        return null;
    }

    /// <summary>
    /// Executes a foreground command in the current thread's session.
    /// </summary>
    public async Task<(string output, int exitCode)> ExecuteCommandAsync(
        string command,
        CancellationToken ct = default)
    {
        var session = await GetOrCreateSessionAsync(ct);
        var result = await session.ExecuteCommandAsync(command, TerminalConfiguration.CommandTimeout, ct);
        return result;
    }

    /// <summary>
    /// Executes a background command in the current thread's session.
    /// </summary>
    public async Task<string> ExecuteBackgroundCommandAsync(string command, CancellationToken ct = default)
    {
        var session = await GetOrCreateSessionAsync(ct);
        return await session.ExecuteBackgroundCommandAsync(command);
    }

    /// <summary>
    /// Cancels the currently running command in the current thread's session.
    /// </summary>
    public void CancelCurrentCommand()
    {
        GetCurrentSession()?.CancelCurrentCommand();
    }

    /// <summary>
    /// Gets the last command run in the current thread's session.
    /// </summary>
    public string? GetLastCommand()
    {
        return GetCurrentSession()?.LastCommand;
    }

    /// <summary>
    /// Gets terminal state formatted for context injection.
    /// Shows just the last foreground command with its exit code.
    /// Returns a message about empty terminal if no commands have been run.
    /// </summary>
    public string GetTerminalStateForContext()
    {
        var session = GetCurrentSession();
        if (session == null)
        {
            return "Terminal: No active terminal session.";
        }

        var sb = new StringBuilder();
        sb.AppendLine($"Terminal: {session.Id}");

        // Show last command info, or indicate no commands have been run
        if (string.IsNullOrEmpty(session.LastCommand))
        {
            sb.AppendLine("Last Command: (no commands run yet)");
        }
        else
        {
            sb.AppendLine($"Last Command: {session.LastCommand}");
            sb.AppendLine($"Exit Code: {session.LastExitCode?.ToString() ?? "(unknown)"}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string GetShellPath()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // Git Bash on Windows
            var gitBash = @"C:\Program Files\Git\bin\bash.exe";
            if (File.Exists(gitBash))
            {
                return gitBash;
            }

            throw new InvalidOperationException("Git Bash not found at " + gitBash);
        }

        return "/bin/bash";
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        _disposing = true;

        if (disposing)
        {
            foreach (var session in _sessions.Values)
            {
                session.Dispose();
            }

            _sessions.Clear();
        }

        _disposed = true;
    }
}
