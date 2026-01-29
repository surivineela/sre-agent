// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using Agent.Framework;
using Agent.Plugins.Models.WorkspaceTools;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Services;

/// <summary>
/// Local implementation of IBashTool that uses TerminalSessionManager
/// for local command execution.
/// </summary>
public class LocalBashTools : IBashTool
{
    private readonly ILogger _logger;
    private readonly TerminalSessionManager _terminalManager;

    /// <summary>
    /// Initializes a new instance of the LocalBashTools class.
    /// </summary>
    /// <param name="logger">Logger for diagnostic output.</param>
    /// <param name="sandboxRoot">The root directory for terminal operations.</param>
    public LocalBashTools(ILogger logger, string sandboxRoot)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _terminalManager = new TerminalSessionManager(logger, sandboxRoot);
    }

    /// <inheritdoc />
    public async Task<string> RunInTerminalAsync(string command, string explanation, bool isBackground)
    {
        try
        {
            if (isBackground)
            {
                var result = await _terminalManager.ExecuteBackgroundCommandAsync(command);
                return result;
            }
            else
            {
                var (output, exitCode) = await _terminalManager.ExecuteCommandAsync(command);
                return FormatForegroundOutput(output, exitCode);
            }
        }
        catch (TimeoutException)
        {
            return "Tool call failed: Command timed out after 5 minutes";
        }
        catch (TerminalInitializationException ex)
        {
            _logger.LogInternalError(ex, "Failed to initialize terminal session");
            return $"Tool call failed: {ex.Message}";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error running command in terminal: {Command}", command);
            return $"Tool call failed: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public Task<string> GetTerminalLastCommandAsync()
    {
        var lastCommand = _terminalManager.GetLastCommand();
        if (string.IsNullOrEmpty(lastCommand))
        {
            return Task.FromResult("Tool call failed: No active terminal session or no command has been run");
        }

        return Task.FromResult(lastCommand);
    }

    /// <inheritdoc />
    public async Task<string> GetBackgroundTaskOutputAsync(string taskId, bool block = true, int timeout = 30000)
    {
        if (string.IsNullOrEmpty(taskId))
        {
            return "Tool call failed: taskId is required";
        }

        try
        {
            var session = _terminalManager.GetCurrentSession();
            if (session == null)
            {
                return "Tool call failed: No active terminal session";
            }

            var (outputFile, pid) = session.GetLastBackgroundProcessInfo();
            if (string.IsNullOrEmpty(outputFile))
            {
                return "Tool call failed: No background task output file found";
            }

            if (!File.Exists(outputFile))
            {
                return "Tool call failed: Background task output file does not exist";
            }

            // Check if process is still running
            var isRunning = false;
            if (pid.HasValue)
            {
                try
                {
                    var process = System.Diagnostics.Process.GetProcessById(pid.Value);
                    isRunning = !process.HasExited;
                }
                catch (ArgumentException)
                {
                    // Process not found, it has exited
                }
            }

            if (block && isRunning)
            {
                // Wait for process to complete or timeout
                var startTime = DateTime.UtcNow;
                while (isRunning && (DateTime.UtcNow - startTime).TotalMilliseconds < timeout)
                {
                    await Task.Delay(100);
                    try
                    {
                        var process = System.Diagnostics.Process.GetProcessById(pid!.Value);
                        isRunning = !process.HasExited;
                    }
                    catch (ArgumentException)
                    {
                        isRunning = false;
                    }
                }
            }

            var output = await File.ReadAllTextAsync(outputFile);
            var result = new StringBuilder();

            if (!string.IsNullOrWhiteSpace(output))
            {
                result.AppendLine(output.Trim());
            }

            if (isRunning)
            {
                result.AppendLine("\nTask is still running.");
            }

            return result.Length > 0 ? result.ToString().Trim() : "No output available.";
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting background task output");
            return $"Tool call failed: {ex.Message}";
        }
    }

    /// <inheritdoc />
    public string GetTerminalStateForContext() => _terminalManager.GetTerminalStateForContext();

    /// <summary>
    /// Formats foreground command output with exit code.
    /// </summary>
    private static string FormatForegroundOutput(string output, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(output))
        {
            return $"Command completed with exit code {exitCode}";
        }

        return $"{output}\n\nExit code: {exitCode}";
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _terminalManager.Dispose();
        GC.SuppressFinalize(this);
    }
}
