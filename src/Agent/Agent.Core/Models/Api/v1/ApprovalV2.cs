// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record ApprovalV2(
    Guid Id,
    Guid AgentContextId,
    Guid ThreadId,
    string Title,
    ApprovalDecision Status,
    DateTime CreatedTimestamp,
    DateTime? DecisionTimestamp,
    string? DecisionUserId
);
