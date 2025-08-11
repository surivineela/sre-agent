// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

public record AgentTaskDocument(
    string Id,
    string ThreadId,
    string Title,
    IEnumerable<AgentTaskStep>? Steps,
    AgentTaskProperties? Properties,
    AgentTaskStatus Status,
    AgentTaskType Type,
    AgentTaskInputData? InputData,
    DateTime? LastModified
) : ICosmosDocument
{
    public string DocumentType => "AgentTask";
    public string PartitionKey => ThreadId;
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public static AgentTaskDocument FromDomainModel(AgentTask agentTask) =>
        new(
            Id: agentTask.Id.ToString(),
            ThreadId: agentTask.ThreadId.ToString(),
            Title: agentTask.Title,
            Steps: agentTask.Steps,
            Properties: agentTask.Properties,
            Status: agentTask.Status,
            Type: agentTask.Type,
            InputData: agentTask.InputData,
            LastModified: agentTask.LastModified
        );

    public AgentTask ToDomainModel() =>
        new()
        {
            Id = Guid.Parse(Id),
            Title = Title,
            Steps = Steps,
            Properties = Properties,
            Status = Status,
            Type = Type,
            ThreadId = Guid.Parse(ThreadId),
            InputData = InputData,
            LastModified = LastModified
        };
}
