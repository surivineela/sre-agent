// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

// Extended AgentContext model for Cosmos DB
public record AgentContextDocument(
    string Id,
    string ThreadId,
    AgentTypeEnum AgentType,
    ContextStateEnum ContextState,
    WaitInformation? WaitInformation,
    ApprovalInformation? ApprovalInformation,
    string? AssignedInstanceId = null,
    DateTimeOffset? AssignmentExpires = null
) : ICosmosDocument
{
    public string DocumentType => "AgentContext";
    public string PartitionKey => ThreadId; // Use Thread Id as partition key
    public static string ContainerName => AgentDataConfiguration.AgentContextContainerName;

    public static string AssignedInstancePatchPath => "/assignedInstanceId";
    public static string AssignmentExpiresPatchPath => "/assignmentExpires";

    // Conversion to/from domain model
    public static AgentContextDocument FromDomainModel(AgentContext agentContext) =>
        new(
            Id: agentContext.Id.ToString(),
            ThreadId: agentContext.ThreadId.ToString(),
            AgentType: agentContext.AgentType,
            ContextState: agentContext.ContextState,
            WaitInformation: agentContext.WaitInformation,
            ApprovalInformation: agentContext.ApprovalInformation,
            AssignedInstanceId: agentContext.AssignedInstanceId,
            AssignmentExpires: agentContext.AssignmentExpires
        );

    public AgentContext ToDomainModel() =>
        new(
            Id: Guid.Parse(Id),
            ThreadId: Guid.Parse(ThreadId),
            AgentType: AgentType,
            ContextState: ContextState,
            WaitInformation: WaitInformation,
            ApprovalInformation: ApprovalInformation,
            AssignedInstanceId: AssignedInstanceId,
            AssignmentExpires: AssignmentExpires
        );
}
