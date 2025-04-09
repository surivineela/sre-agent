// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Models.Api.v1
{
    public enum ThreadSource
    {
        Portal, // Chat scenario, from Azure Portal or any web client through API
        Agent,  // Agent proactively created thread, e.g. daily report
        Teams,  // Agent tagged in teams channel, chat group or direct message
        Alert,  // Agent invoked by alert or IcM webhook
    }

    public record Thread(
        Guid Id,
        string Title,
        Message StartMessage,
        Message LastMessage,
        DateTime CreatedTimestamp,
        DateTime ModifiedTimestamp,
        ThreadSource Source = ThreadSource.Portal
        );

    public record CreateThreadRequest(
        [Required] CreateMessageRequest StartMessage
    );

    public record CreateMessageRequest(
        [Required] string Text,
        string UserId,
        string DisplayName
    );
}

