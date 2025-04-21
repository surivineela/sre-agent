// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

// Extended AgentContext model for Cosmos DB
public record ApprovalV2Document(
    string Id,
    string AgentContextId,
    string ThreadId,
    string Title,
    int Status,
    DateTime CreatedTimestamp,
    DateTime? DecisionTimestamp,
    string? DecisionUserId
) : ICosmosDocument
{
    public string DocumentType => "ApprovalV2";
    public string PartitionKey => AgentContextId; // Use Agent Context Id as partition key
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    // Conversion to/from domain model
    public static ApprovalV2Document FromDomainModel(ApprovalV2 approvalV2) =>
        new(
            Id: approvalV2.Id.ToString(),
            AgentContextId: approvalV2.AgentContextId.ToString(),
            ThreadId: approvalV2.ThreadId.ToString(),
            Title: approvalV2.Title,
            Status: (int)approvalV2.Status,
            CreatedTimestamp: approvalV2.CreatedTimestamp,
            DecisionTimestamp: approvalV2.DecisionTimestamp,
            DecisionUserId: approvalV2.DecisionUserId
        );

    public ApprovalV2 ToDomainModel() =>
        new(
            Id: Guid.Parse(Id),
            AgentContextId: Guid.Parse(AgentContextId),
            ThreadId: Guid.Parse(ThreadId),
            Title: Title,
            Status: (ApprovalDecision)Status,
            CreatedTimestamp: CreatedTimestamp,
            DecisionTimestamp: DecisionTimestamp,
            DecisionUserId: DecisionUserId
        );
}
