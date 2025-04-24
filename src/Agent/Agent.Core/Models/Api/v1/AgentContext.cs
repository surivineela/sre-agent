// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record AgentContext(
    Guid Id,
    Guid ThreadId,
    AgentTypeEnum AgentType,
    ContextStateEnum ContextState,
    WaitInformation? WaitInformation,
    ApprovalInformation? ApprovalInformation,
    string? AssignedInstanceId = null,
    DateTimeOffset? AssignmentExpires = null,
    string? InputDataSerialized = null,
    AgentTypeEnum? HandoffFromAgentType = null,
    ContextStateEnum? HandoffState = null
);
