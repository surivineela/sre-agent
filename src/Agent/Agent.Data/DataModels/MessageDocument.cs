// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

// Extended Message model for Cosmos DB
public record MessageDocument(
    string Id,
    string ThreadId,
    DateTime TimeStamp,
    Author Author,
    string Text,
    bool IsImageContent = false,
    Posted? Posted = null
) : ICosmosDocument
{
    public string DocumentType => "Message";
    public string PartitionKey => ThreadId; // Use Thread Id as partition key to keep messages with their thread
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    // Conversion to/from domain model
    public static MessageDocument FromDomainModel(Message message, string threadId) =>
        new MessageDocument(
            message.Id.ToString(),
            threadId,
            message.TimeStamp,
            new Author(message.Author.Role, message.Author.UserId, message.Author.DisplayName),
            message.Text,
            message.IsImageContent,
            message.Posted
        );

    public Message ToDomainModel() =>
        new Message(
            Guid.Parse(Id),
            TimeStamp,
            Author,
            Text,
            IsImageContent,
            Posted
        );
}
