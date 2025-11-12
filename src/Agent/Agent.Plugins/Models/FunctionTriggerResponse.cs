// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Models;

/// <summary>
/// Represents the response from triggering a non-HTTP Azure Function
/// </summary>
public record FunctionTriggerResponse(
    bool Success,
    string? StatusCode,
    string? ResponseContent,
    string? ErrorMessage,
    TimeSpan? Duration
);
