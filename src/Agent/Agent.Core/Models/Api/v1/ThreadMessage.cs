// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record ThreadMessage(
    Guid ThreadId,
    Guid AgentContextId,
    Guid MessageId,
    string Message,
    string UserId,
    string DisplayName,
    DateTime Timestamp,
    Posted? Posted = null,
    string? AgentName = null,
    ConversationModifierEnum? ConversationModifier = null);

public record InboundServiceResponse(
    Guid ThreadId,
    Guid MessageId,
    bool Busy = false // The thread is processing the message and cannot accept new messages at this time
);
