// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1;

/// <summary>
/// Represents a Task tool execution that spawns a subagent for specialized tasks.
/// </summary>
public class TaskToolExecution
{
    /// <summary>
    /// Unique identifier for this execution (matches the FunctionCall.CallId).
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Short description of what the subagent will do (3-5 words).
    /// </summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The type of subagent being spawned.
    /// </summary>
    public string SubagentType { get; set; } = string.Empty;

    /// <summary>
    /// The task prompt given to the subagent.
    /// Excluded from Cosmos when null to minimize storage for historical records.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Prompt { get; set; }

    /// <summary>
    /// Current execution status.
    /// </summary>
    public TaskToolExecutionStatus Status { get; set; } = TaskToolExecutionStatus.Running;

    /// <summary>
    /// When the execution started.
    /// </summary>
    public DateTime StartedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When the execution completed (if finished).
    /// </summary>
    public DateTime? CompletedAt { get; set; }

    /// <summary>
    /// The result/response from the subagent (if completed).
    /// </summary>
    public string? Result { get; set; }

    /// <summary>
    /// Error message if the execution failed.
    /// </summary>
    public string? Error { get; set; }
}

/// <summary>
/// Status of a Task tool execution.
/// </summary>
public enum TaskToolExecutionStatus
{
    /// <summary>
    /// Task is pending start.
    /// </summary>
    Pending,

    /// <summary>
    /// Task is currently executing.
    /// </summary>
    Running,

    /// <summary>
    /// Task completed successfully.
    /// </summary>
    Completed,

    /// <summary>
    /// Task execution failed.
    /// </summary>
    Failed,

    /// <summary>
    /// Task execution was cancelled.
    /// </summary>
    Cancelled
}

/// <summary>
/// Represents a group of parallel Task tool executions.
/// When multiple subagents are spawned in parallel, they're grouped together.
/// </summary>
public class TaskToolExecutionGroup
{
    /// <summary>
    /// Unique identifier for this group.
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// Individual subagent executions in this group.
    /// </summary>
    public List<TaskToolExecution> Executions { get; set; } = new();

    /// <summary>
    /// Whether all executions in the group have completed.
    /// </summary>
    public bool IsComplete => Executions.All(e =>
        e.Status == TaskToolExecutionStatus.Completed ||
        e.Status == TaskToolExecutionStatus.Failed ||
        e.Status == TaskToolExecutionStatus.Cancelled);

    /// <summary>
    /// When the group started (earliest startedAt).
    /// </summary>
    public DateTime StartedAt => Executions.Min(e => e.StartedAt);

    /// <summary>
    /// When the group completed (latest completedAt).
    /// </summary>
    public DateTime? CompletedAt => IsComplete
        ? Executions.Max(e => e.CompletedAt)
        : null;
}
