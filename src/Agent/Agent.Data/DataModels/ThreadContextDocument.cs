// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

// Extended Thread model for Cosmos DB
public record ThreadContextDocument(
    string Id,
    string ThreadId,
    List<Message> Messages
) : ICosmosDocument
{
    public string DocumentType => "ThreadContext";
    public string PartitionKey => Id; // Use Thread Id as partition key

    // Conversion to/from domain model
    public static ThreadContextDocument FromDomainModel(ThreadContext threadContext) =>
        new ThreadContextDocument(
            GetId(threadContext.ThreadId.ToString()),
            threadContext.ThreadId.ToString(),
            Messages: threadContext.RecentMessages.ToList()
        );

    public ThreadContext ToDomainModel() =>
        new ThreadContext(
            Guid.Parse(ThreadId),
            new Queue<Message>(Messages)
        );

    public static string GetId(string threadId)
    {
        return $"context-{threadId}";
    }
}
