// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1
{
    public enum ApprovalDecision
    {
        Pending,
        Approved,
        Rejected
    }

    public record Approval(
        string Id,
        string Title,
        ApprovalDecision Status,
        DateTime CreatedTimestamp,
        DateTime? DecisionTimestamp,
        string? decisionUserId
        )
    {
    }
}

