// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

public record AgentContextInstanceAssignmentDocument(
    string AgentContextId,
    string ThreadId,
    string InstanceId,
    DateTimeOffset Expires
) : ICosmosDocument
{
    public string Id => GenerateId(AgentContextId, InstanceId);
    public string DocumentType => "AgentContextInstanceAssignment";
    public string PartitionKey => InstanceId;
    public static string ContainerName => AgentDataConfiguration.InstanceAssignmentsContainerName;

    public AgentContextInstanceAssignment ToDomainModel() =>
        new(AgentContextId, ThreadId, InstanceId, Expires);

    public static AgentContextInstanceAssignmentDocument FromDomainModel(AgentContextInstanceAssignment assignment) =>
        new(
            assignment.AgentContextId,
            assignment.ThreadId,
            assignment.InstanceId,
            assignment.Expires
        );

    public static string GenerateId(string agentContextId, string instanceId) =>
        $"{agentContextId}_{instanceId}";
}
