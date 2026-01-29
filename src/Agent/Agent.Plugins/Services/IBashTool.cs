// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Services;

/// <summary>
/// Interface for terminal/bash operations in workspace tools.
/// Provides abstraction for both local and remote terminal execution.
/// </summary>
public interface IBashTool : IDisposable
{
    /// <summary>
    /// Executes a command in the terminal.
    /// </summary>
    /// <param name="command">The command to execute.</param>
    /// <param name="explanation">A description of what the command does.</param>
    /// <param name="isBackground">Whether to run as a background process.</param>
    /// <returns>Command output with exit code, or task ID for background processes.</returns>
    Task<string> RunInTerminalAsync(string command, string explanation, bool isBackground);

    /// <summary>
    /// Gets the last command run in the terminal.
    /// </summary>
    /// <returns>The last command or error message.</returns>
    Task<string> GetTerminalLastCommandAsync();

    /// <summary>
    /// Gets the output of a background task.
    /// </summary>
    /// <param name="taskId">The task ID from a previous background command.</param>
    /// <param name="block">Whether to wait for completion.</param>
    /// <param name="timeout">Maximum wait time in milliseconds.</param>
    /// <returns>Task output or error message.</returns>
    Task<string> GetBackgroundTaskOutputAsync(string taskId, bool block = true, int timeout = 30000);

    /// <summary>
    /// Gets terminal state formatted for context injection.
    /// </summary>
    /// <returns>Terminal state summary.</returns>
    string GetTerminalStateForContext();
}
