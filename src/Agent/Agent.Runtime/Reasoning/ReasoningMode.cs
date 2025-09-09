// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Reasoning mode for RCARouterAgent sessions.
/// Workflow: RCA workflow orchestration (default)
/// Conversation: Free conversation via conversation_router_agent.
/// </summary>
public enum ReasoningMode
{
    Workflow = 0,
    Conversation = 1
}
