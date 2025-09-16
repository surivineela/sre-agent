// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

public record ScheduledTaskDocument(
    string Id,
    string Name,
    string Description,
    string CronExpression,
    DateTime StartTime,
    DateTime? EndTime,
    string AgentPrompt,
    string? Agent,
    string? ThreadId,
    string CreatedBy,
    DateTime CreatedAt,
    DateTime? LastExecutionTime,
    ScheduledTaskStatus Status,
    Dictionary<string, object>? ExecutionContext,
    List<ScheduledTaskExecution>? ExecutionHistory,
    int? MaxExecutions,
    int ExecutionCount,
    string? NotificationChannel
) : ICosmosDocument
{
    public string DocumentType => "ScheduledTask";
    public string PartitionKey => Id;
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public string Name { get; set; } = Name;
    public string Description { get; set; } = Description;
    public string CronExpression { get; set; } = CronExpression;
    public DateTime StartTime { get; set; } = StartTime;
    public DateTime? EndTime { get; set; } = EndTime;
    public string AgentPrompt { get; set; } = AgentPrompt;
    public string? Agent { get; set; } = Agent;
    public string? ThreadId { get; set; } = ThreadId;
    public string CreatedBy { get; set; } = CreatedBy;
    public DateTime CreatedAt { get; set; } = CreatedAt;
    public DateTime? LastExecutionTime { get; set; } = LastExecutionTime;
    public ScheduledTaskStatus Status { get; set; } = Status;
    public Dictionary<string, object>? ExecutionContext { get; set; } = ExecutionContext;
    public List<ScheduledTaskExecution>? ExecutionHistory { get; set; } = ExecutionHistory;
    public int? MaxExecutions { get; set; } = MaxExecutions;
    public int ExecutionCount { get; set; } = ExecutionCount;
    public string? NotificationChannel { get; set; } = NotificationChannel;
}

public enum ScheduledTaskStatus
{
    Active,
    Paused,
    Completed,
    Failed,
    Expired
}

public record ScheduledTaskExecution(
    DateTime ExecutionTime,
    string? ThreadId,
    bool Success,
    string? ErrorMessage,
    Dictionary<string, object>? ExecutionMetadata
);

public record CreateScheduledTaskRequest(
    string Name,
    string Description,
    string CronExpression,
    DateTime StartTime,
    DateTime? EndTime,
    string AgentPrompt,
    string? Agent,
    string? ThreadId,
    string CreatedBy,
    Dictionary<string, object>? ExecutionContext = null,
    int? MaxExecutions = null,
    string? NotificationChannel = null
);

public record UpdateScheduledTaskRequest(
    string? Name = null,
    string? Description = null,
    string? CronExpression = null,
    DateTime? StartTime = null,
    DateTime? EndTime = null,
    string? AgentPrompt = null,
    ScheduledTaskStatus? Status = null,
    Dictionary<string, object>? ExecutionContext = null,
    int? MaxExecutions = null,
    string? NotificationChannel = null
);

public record ScheduledTaskCreationResult(
    bool Success,
    string? TaskId = null,
    string? Message = null
);

public record ScheduledTaskSummary(
    string Id,
    string Name,
    string Description,
    ScheduledTaskStatus Status,
    DateTime? LastExecutionTime,
    int ExecutionCount
);
