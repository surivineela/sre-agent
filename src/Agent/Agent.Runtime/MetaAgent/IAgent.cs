// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.MetaAgent;

public interface IAgent
{
    Task<string> ProcessUserMessageAsync(AgentContext agentContext, AgentChatHistory agentChatHistory);
}
