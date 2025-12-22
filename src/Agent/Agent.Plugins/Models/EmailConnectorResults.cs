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

    public string? BodyContentType { get; init; }
    public string? BodyContent { get; init; }

    /// <summary>
    /// Gets the original length of the body content before truncation, if any.
    /// This property is null for list operations where body content is excluded.
    /// </summary>
    public int? BodyContentLength { get; init; }

    /// <summary>
    /// Gets a value indicating whether the body content was truncated due to size limits.
    /// </summary>
    public bool IsBodyContentTruncated { get; init; }

    public JsonElement RawPayload { get; init; }
}

public record EmailMessageResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string ResponseContent { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public EmailMessage? Email { get; init; }

    /// <summary>
    /// Gets the total size of the API response in bytes.
    /// </summary>
    public int ResponseContentSizeBytes { get; init; }
}

public record EmailListResult
{
    public bool Success { get; init; }
    public int StatusCode { get; init; }
    public string ResponseContent { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public IReadOnlyList<EmailMessage> Emails { get; init; } = Array.Empty<EmailMessage>();
    public string? ContinuationToken { get; init; }

    /// <summary>
    /// Gets the total size of the API response in bytes.
    /// </summary>
    public int ResponseContentSizeBytes { get; init; }
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
