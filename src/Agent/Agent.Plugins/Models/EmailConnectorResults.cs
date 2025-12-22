// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.Json;

namespace Agent.Plugins.Models;

public record EmailMessage
{
    public string Id { get; init; } = string.Empty;
    public string? Subject { get; init; }
    public string? From { get; init; }
    public IReadOnlyList<string> ToRecipients { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> CcRecipients { get; init; } = Array.Empty<string>();
    public bool? IsRead { get; init; }
    public string? Importance { get; init; }
    public DateTimeOffset? ReceivedDateTime { get; init; }
    public string? BodyPreview { get; init; }
    public string? BodyContentType { get; init; }
    public string? BodyContent { get; init; }
    public JsonElement RawPayload { get; init; }
}

public record EmailMessageResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string ResponseContent { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public EmailMessage? Email { get; init; }
}

public record EmailListResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string ResponseContent { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<EmailMessage> Emails { get; init; } = Array.Empty<EmailMessage>();
    public string? ContinuationToken { get; init; }
}

public record EmailReplyResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string ResponseContent { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}

public record EmailMoveResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string ResponseContent { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
}
