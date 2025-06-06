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
    Guid? HandoffToAgentContextId = null, // agent context this agent is handing off to
    Guid? HandoffFromAgentContextId = null, // agent context this agent was handed off from
    string? CurrentAgent = null, // current agent handling this context. Deprecated in favor of AgentStack
    List<string>? AgentHandoffChain = null // handoff stack of agents
)
{
    public List<string> AgentHandoffChain { get; init; } = AgentHandoffChain ?? new List<string>();
}
