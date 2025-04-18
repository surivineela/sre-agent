// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

// Extended Message Feedback model for Cosmos DB
public record ReasoningMessageDocument(
    string Id,
    string AgentContextId,
    int Role,
    string? SerializedChatMessage
) : ICosmosDocument
{
    public string DocumentType => "ReasoningMessage";
    public string PartitionKey => AgentContextId; // Use AgentContext Id as partition key to keep reasoning messages with their subagent thread

    // Conversion to/from domain model
    public static ReasoningMessageDocument FromDomainModel(ReasoningMessage reasoningMessage) =>
        new ReasoningMessageDocument(
            Id: reasoningMessage.Id.ToString(),
            AgentContextId: reasoningMessage.AgentContextId.ToString(),
            Role: (int)reasoningMessage.Role,
            SerializedChatMessage: reasoningMessage.SerializedChatMessage
        );

    public ReasoningMessage ToDomainModel() =>
        new ReasoningMessage(
            Id: Guid.Parse(Id),
            AgentContextId: Guid.Parse(AgentContextId),
            Role: (ReasoningMessageRoleEnum)Role,
            SerializedChatMessage: SerializedChatMessage
        );
}
