// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

// Extended Thread model for Cosmos DB
public record ThreadContextDocument(
    string Id,
    string ThreadId,
    int AgentType,
    List<Message> Messages,
    bool IsThreadActive,
    OutboundConfiguration? OutboundConfiguration = null
) : ICosmosDocument
{
    public string DocumentType => "ThreadContext";
    public string PartitionKey => Id; // Use Thread Id as partition key

    // Conversion to/from domain model
    public static ThreadContextDocument FromDomainModel(ThreadContext threadContext) =>
        new ThreadContextDocument(
            GetId(threadContext.ThreadId.ToString()),
            threadContext.ThreadId.ToString(),
            AgentType: (int)threadContext.AgentTypeEnum,
            Messages: threadContext.RecentMessages.ToList(),
            IsThreadActive: threadContext.IsThreadActive,
            OutboundConfiguration: threadContext.OutboundConfiguration
        );

    public ThreadContext ToDomainModel() =>
        new ThreadContext(
            Guid.Parse(ThreadId),
            (AgentTypeEnum)AgentType,
            IsThreadActive,
            Messages == null ? new Queue<Message>() : new Queue<Message>(Messages),
            OutboundConfiguration
        );

    public static string GetId(string threadId)
    {
        return $"context-{threadId}";
    }
}
