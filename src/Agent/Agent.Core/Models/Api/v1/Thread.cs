// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Models.Api.v1
{
    public enum ThreadSource
    {
        Portal,        // Legacy type for portal chat conversations (keeping for backward compatibility)
        Conversation,  // New default type for regular chat conversations
        Agent,         // Agent proactively created thread, e.g. daily report
        Teams,         // Agent tagged in teams channel, chat group or direct message
        Alert,         // Agent invoked by alert or IcM webhook
        Incident       // For incident/security related threads
    }

    public record Thread(
        Guid Id,
        string Title,
        Message StartMessage,
        Message LastMessage,
        DateTime CreatedTimestamp,
        DateTime ModifiedTimestamp,
        ThreadSource Source = ThreadSource.Conversation
    );

    public record CreateThreadRequest(
        [Required] CreateMessageRequest StartMessage,
        ThreadSource? Source = ThreadSource.Conversation  // New threads default to Conversation
    );

    public record CreateMessageRequest(
        [Required] string Text,
        string UserId,
        string DisplayName
    );

    public record FeedbackRequest(
        [Required] bool IsPositive,
        string? FeedbackText
    );
}

