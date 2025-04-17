// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Data.DataModels;

// Extended Message Feedback model for Cosmos DB
public record MessageFeedbackDocument(
    string Id,
    string ThreadId,
    DateTime TimeStamp,
    List<Message> Messages,
    bool IsPositiveFeedback,
    string FeedbackText,
    string? RootCause
) : ICosmosDocument
{
    public string DocumentType => "MessageFeedback";
    public string PartitionKey => ThreadId; // Use Thread Id as partition key to keep messages with their thread

    // Conversion to/from domain model
    public static MessageFeedbackDocument FromDomainModel(MessageFeedback messageFeedback, string threadId) =>
        new MessageFeedbackDocument(
            messageFeedback.Id.ToString(),
            threadId,
            messageFeedback.TimeStamp,
            messageFeedback.Messages,
            messageFeedback.IsPositiveFeedback,
            messageFeedback.FeedbackText,
            messageFeedback.RootCause
        );

    public MessageFeedback ToDomainModel() =>
        new MessageFeedback(
            Guid.Parse(Id),
            Guid.Parse(ThreadId),
            TimeStamp,
            Messages,
            IsPositiveFeedback,
            FeedbackText,
            RootCause
        );
}

