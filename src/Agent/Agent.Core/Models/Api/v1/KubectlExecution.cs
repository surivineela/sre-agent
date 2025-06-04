namespace Agent.Core.Models.Api.v1;

public record KubectlExecution(
    Guid Id,
    string Command,
    string Description,
    KubectlExecutionStatus Status,
    string ClusterResourceId,
    string? Output,
    string? Error,
    DateTime CreatedTimestamp,
    DateTime? StartedTimestamp,
    DateTime? CompletedTimestamp,
    Author? ExecutedBy,
    Guid? AgentContextId
);

public enum KubectlExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled
}
