// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Framework.TaskTool;

/// <summary>
/// Represents a single Task tool execution within a group.
/// </summary>
public class TaskToolExecution
{
    /// <summary>
    /// Unique identifier for this execution (matches FunctionCallContent.CallId).
    /// </summary>
    public required string ExecutionId { get; init; }

    /// <summary>
    /// Short description of what the subagent will do (3-5 words).
    /// </summary>
    public string Description { get; init; } = string.Empty;

    /// <summary>
    /// The type of subagent being spawned.
    /// </summary>
    public SubAgentType SubAgentType { get; init; }

    /// <summary>
    /// The task prompt given to the subagent.
    /// </summary>
    public string? Prompt { get; init; }

    /// <summary>
    /// Current execution status.
    /// </summary>
    public TaskToolExecutionStatus Status { get; set; } = TaskToolExecutionStatus.Pending;

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
/// When multiple subagents are spawned in parallel (same model response), they're grouped together.
/// </summary>
public class TaskToolExecutionGroup
{
    /// <summary>
    /// Unique identifier for this group.
    /// </summary>
    public string GroupId { get; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Individual subagent executions in this group.
    /// </summary>
    public List<TaskToolExecution> Executions { get; } = new();

    /// <summary>
    /// The function calls that triggered these executions.
    /// </summary>
    public List<FunctionCallContent> FunctionCalls { get; } = new();

    /// <summary>
    /// The TaskTool instances for each execution.
    /// </summary>
    internal List<TaskTool<object>> TaskTools { get; } = new();

    /// <summary>
    /// Whether all executions in the group have completed.
    /// </summary>
    public bool IsComplete => Executions.All(e =>
        e.Status is TaskToolExecutionStatus.Completed or
                    TaskToolExecutionStatus.Failed or
                    TaskToolExecutionStatus.Cancelled);

    /// <summary>
    /// When the group started (earliest startedAt).
    /// </summary>
    public DateTime StartedAt => Executions.Count > 0
        ? Executions.Min(e => e.StartedAt)
        : DateTime.UtcNow;

    /// <summary>
    /// When the group completed (latest completedAt).
    /// </summary>
    public DateTime? CompletedAt => IsComplete
        ? Executions.Max(e => e.CompletedAt)
        : null;

    /// <summary>
    /// Adds a Task tool execution to this group.
    /// </summary>
    public void AddExecution(
        FunctionCallContent functionCall,
        SubAgentType subAgentType,
        string? description,
        string? prompt)
    {
        FunctionCalls.Add(functionCall);
        Executions.Add(new TaskToolExecution
        {
            ExecutionId = functionCall.CallId,
            SubAgentType = subAgentType,
            Description = description ?? subAgentType.ToString(),
            Prompt = prompt,
            Status = TaskToolExecutionStatus.Pending,
            StartedAt = DateTime.UtcNow
        });
    }

    /// <summary>
    /// Updates the status of an execution by its ID.
    /// </summary>
    public void UpdateExecutionStatus(string executionId, TaskToolExecutionStatus status, string? result = null, string? error = null)
    {
        var execution = Executions.FirstOrDefault(e => e.ExecutionId == executionId);
        if (execution != null)
        {
            execution.Status = status;
            if (status is TaskToolExecutionStatus.Completed or TaskToolExecutionStatus.Failed or TaskToolExecutionStatus.Cancelled)
            {
                execution.CompletedAt = DateTime.UtcNow;
            }
            if (result != null) execution.Result = result;
            if (error != null) execution.Error = error;
        }
    }
}
