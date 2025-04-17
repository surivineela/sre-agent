// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Data.DataModels;

// Extended SubAgentThread model for Cosmos DB
public record SubAgentThreadDocument(
    string Id,
    string ThreadId,
    int AgentType
) : ICosmosDocument
{

    public string DocumentType => "SubAgentThread";
    public string PartitionKey => ThreadId; // Use Thread Id as partition key

    // Conversion to/from domain model
    public static SubAgentThreadDocument FromDomainModel(SubAgentThread subAgentThread) =>
        new SubAgentThreadDocument(
            Id: subAgentThread.Id.ToString(),
            ThreadId: subAgentThread.ThreadId.ToString(),
            AgentType: (int) subAgentThread.AgentType
        );

    public SubAgentThread ToDomainModel() =>
        new SubAgentThread(
            Id: Guid.Parse(Id),
            ThreadId: Guid.Parse(ThreadId),
            AgentType: (AgentTypeEnum)AgentType
        );
}

