// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Common.Services;

/// <summary>
/// Represents a todo item for workspace task tracking.
/// </summary>
public record WorkspaceTodoItemDto(int Id, string Title, string? Description, string Status);

/// <summary>
/// Interface for workspace context operations.
/// Provides context for agent instructions, environment, and pre-query state.
/// Used by both local (WorkspaceToolsPlugin) and remote (gRPC server) implementations.
/// </summary>
public interface IWorkspaceContext
{
    /// <summary>
    /// Gets instructions context containing copilot-instructions.md, AGENTS.md, and *.instructions.md files.
    /// </summary>
    Task<string> GetInstructionsContextAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets environment context containing OS info and workspace folder structure.
    /// </summary>
    Task<string> GetEnvironmentContextAsync(CancellationToken ct = default);

    /// <summary>
    /// Gets pre-query context containing git repos, date, terminal state, and reminders.
    /// Terminal state and todo list are passed explicitly so this can be called from gRPC server
    /// which doesn't have direct access to plugin state.
    /// </summary>
    /// <param name="terminalState">Current terminal state string, or null if not available.</param>
    /// <param name="todoList">Todo list items, or null if not available.</param>
    /// <param name="ct">Cancellation token.</param>
    Task<string> GetPreUserQueryContextAsync(
        string? terminalState,
        IReadOnlyList<WorkspaceTodoItemDto>? todoList,
        CancellationToken ct = default);
}
