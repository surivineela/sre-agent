// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Models;

public record EmailSendResult
{
    public bool Success { get; init; }

    public int StatusCode { get; init; }

    public string ResponseContent { get; init; } = string.Empty;

    public string Message { get; init; } = string.Empty;
}
