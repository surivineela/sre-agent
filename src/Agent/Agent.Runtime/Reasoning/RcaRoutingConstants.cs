// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Central constants for RCARouterAgent routing & mode switching. Limited in scope to RCA feature.
/// </summary>
internal static class RcaRoutingConstants
{
    internal const string AgentType = "RCARouterAgent";
    internal const string WorkflowRootAgent = "rca_router_meta_agent";
    internal const string ConversationRootAgent = "conversation_router_agent";
}
