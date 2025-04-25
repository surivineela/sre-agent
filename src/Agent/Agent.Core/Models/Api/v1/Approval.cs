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
        Guid Id,
        string ThreadId,
        string OrchestrationId,
        string Title,
        string Description,
        ApprovalDecision Status,
        DateTime CreatedTimestamp,
        DateTime? DecisionTimestamp,
        string? OboToken,
        Author? DecisionUser
        )
    {
    }
}

