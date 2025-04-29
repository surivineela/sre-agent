// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Thread = Agent.Core.Models.Api.v1.Thread;

namespace Agent.Data.DataModels;

// Extended Thread model for Cosmos DB
public record ThreadDocument(
    string Id,
    string Title,
    string MessageId, // Reference to the start message
    string LastMessageId, // Reference to the last message
    DateTime CreatedTimestamp,
    DateTime ModifiedTimestamp,
    ThreadSource Source = ThreadSource.Conversation
) : ICosmosDocument
{
    public string DocumentType => "Thread";
    public string PartitionKey => Id; // Use Thread Id as partition key
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;
    public string IncidentId { get; set; } = string.Empty; // Incident Id associated with the thread if the source of the thread is incident

    // Conversion to/from domain model
    public static ThreadDocument FromDomainModel(Thread thread) =>
        new ThreadDocument(
            thread.Id.ToString(),
            thread.Title,
            thread.StartMessage.Id.ToString(),
            thread.LastMessage?.Id.ToString(),
            thread.CreatedTimestamp,
            thread.ModifiedTimestamp,
            thread.Source
        );

    public Thread ToDomainModel(Message startMessage, Message? lastMessage) =>
        new Thread(
            Guid.Parse(Id),
            Title,
            startMessage,
            lastMessage,
            CreatedTimestamp,
            ModifiedTimestamp,
            Source
        );
}
