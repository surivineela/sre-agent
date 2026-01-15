// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Status of a terminal command execution.
/// </summary>
public enum TerminalStatus
{
    /// <summary>
    /// Command is currently running.
    /// </summary>
    Running,

    /// <summary>
    /// Command completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Command failed with an error.
    /// </summary>
    Failed,

    /// <summary>
    /// Command started in background, no result yet.
    /// </summary>
    Background
}

/// <summary>
/// Structured result from a terminal command execution for rich UI rendering.
/// </summary>
public class TerminalExecutionResult
{
    /// <summary>
    /// The command that was executed.
    /// </summary>
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Human-readable explanation of what the command does.
    /// </summary>
    public string? Explanation { get; set; }

    /// <summary>
    /// Whether this command runs in the background.
    /// </summary>
    public bool IsBackground { get; set; }

    /// <summary>
    /// Session ID for background commands.
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Exit code (null for background/running commands).
    /// </summary>
    public int? ExitCode { get; set; }

    /// <summary>
    /// Command stdout output.
    /// </summary>
    public string? Output { get; set; }

    /// <summary>
    /// Command stderr output.
    /// </summary>
    public string? Error { get; set; }

    /// <summary>
    /// Current status of the execution.
    /// </summary>
    public TerminalStatus Status { get; set; }
}
