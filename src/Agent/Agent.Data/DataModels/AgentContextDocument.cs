// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

// Extended AgentContext model for Cosmos DB
public record AgentContextDocument(
    string Id,
    string ThreadId,
    int AgentType,
    int ContextState,
    WaitInformation? WaitInformation,
    ApprovalInformation? ApprovalInformation
) : ICosmosDocument
{
    public string DocumentType => "AgentContext";
    public string PartitionKey => ThreadId; // Use Thread Id as partition key

    // Conversion to/from domain model
    public static AgentContextDocument FromDomainModel(AgentContext agentContext) =>
        new AgentContextDocument(
            Id: agentContext.Id.ToString(),
            ThreadId: agentContext.ThreadId.ToString(),
            AgentType: (int) agentContext.AgentType,
            ContextState: (int)agentContext.ContextState,
            WaitInformation: agentContext.WaitInformation,
            ApprovalInformation: agentContext.ApprovalInformation
        );

    public AgentContext ToDomainModel() =>
        new AgentContext(
            Id: Guid.Parse(Id),
            ThreadId: Guid.Parse(ThreadId),
            AgentType: (AgentTypeEnum)AgentType,
            ContextState: (ContextStateEnum)ContextState,
            WaitInformation: WaitInformation,
            ApprovalInformation: ApprovalInformation
        );
}

