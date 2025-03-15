namespace Agent.Core.Models.Api.v1;
public record ThreadOrchestrationMapping(
    string Id,
    string ThreadId,
    string OrchestrationInstanceId,
    DateTime CreatedTimestamp,
    DateTime ModifiedTimestamp
);
