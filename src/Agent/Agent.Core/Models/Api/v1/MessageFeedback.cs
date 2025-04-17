// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record MessageFeedback(
    Guid Id,
    Guid ThreadId,
    DateTime TimeStamp,
    List<Message> Messages,
    bool IsPositiveFeedback,
    string FeedbackText,
    string? RootCause
)
{
    public MessageFeedback UpdateRootCause(string rootCause)
    {
        return this with { RootCause = rootCause };
    }
}
