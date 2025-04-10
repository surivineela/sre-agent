// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record ThreadMessageFeedback(
    Guid ThreadId,
    Guid MessageFeedbackId,
    bool IsPositive,
    string FeedbackText);

