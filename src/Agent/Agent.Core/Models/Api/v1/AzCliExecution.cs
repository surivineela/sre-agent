using System.Text.Json.Serialization;
using Microsoft.Extensions.AI;

namespace Agent.Core.Models.Api.v1;

public record AzCliExecution(
    Guid Id,
    string Command,
    string Description,
    AzCliExecutionStatus Status,
    string? OriginalFunctionCall,
    string? Output,
    string? Error,
    DateTime CreatedTimestamp,
    DateTime? StartedTimestamp,
    DateTime? CompletedTimestamp,
    Author? ExecutedBy,
    Guid? AgentContextId
);

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum AzCliExecutionStatus
{
    Pending,
    Running,
    Completed,
    Failed,
    Cancelled,
    PendingAuthorization,
}
