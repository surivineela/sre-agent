// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Data.DataModels;

// Extended AgentContext model for Cosmos DB
public record AgentContextDocument(
    string Id,
    string ThreadId,
    int AgentType
) : ICosmosDocument
{
    public string DocumentType => "AgentContext";
    public string PartitionKey => ThreadId; // Use Thread Id as partition key

    // Conversion to/from domain model
    public static AgentContextDocument FromDomainModel(AgentContext agentContext) =>
        new AgentContextDocument(
            Id: agentContext.Id.ToString(),
            ThreadId: agentContext.ThreadId.ToString(),
            AgentType: (int) agentContext.AgentType
        );

    public AgentContext ToDomainModel() =>
        new AgentContext(
            Id: Guid.Parse(Id),
            ThreadId: Guid.Parse(ThreadId),
            AgentType: (AgentTypeEnum)AgentType
        );
}

