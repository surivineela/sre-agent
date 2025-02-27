// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Models
{
    public sealed record RemediationResult(
        bool Success,
        string Action,
        string Details,
        string? OperationId,
        DateTime FinishedTime);
}
