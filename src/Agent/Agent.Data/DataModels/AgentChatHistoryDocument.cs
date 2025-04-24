// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

// Extended AgentChatHistory model for Cosmos DB
public record AgentChatHistoryDocument(
    string AgentContextId,
    List<string> ReasoningMessageIds
) : ICosmosDocument
{
    public string DocumentType => "AgentChatHistory";
    public string Id => GetDocumentId(AgentContextId);
    public string PartitionKey => AgentContextId; // Use AgentContextId as partition key
    public string LatestUserMessageId { get; set; } = string.Empty;
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public static string GetDocumentId(string agentContextId) =>
        $"chathistory-{agentContextId}";

    // Conversion to/from domain model
    public static AgentChatHistoryDocument FromDomainModel(AgentChatHistory agentChatHistory) =>
        new(
            AgentContextId: agentChatHistory.AgentContextId.ToString(),
            ReasoningMessageIds: agentChatHistory.ReasoningMessageIds.Select(m => m.ToString()).ToList()
        )
        {
            LatestUserMessageId = agentChatHistory.LatestUserMessageId.ToString()
        };

    public AgentChatHistory ToDomainModel() =>
        new(
            AgentContextId: Guid.Parse(AgentContextId),
            ReasoningMessageIds: ReasoningMessageIds.Select(m => Guid.Parse(m)).ToList()
        )
        {
            LatestUserMessageId = Guid.Parse(LatestUserMessageId)
        };
}

