// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.MetaAgent;

public interface IAgent
{
    Task<string> ProcessUserMessageAsync(AgentContext agentContext, AgentChatHistory agentChatHistory);

    IAsyncEnumerable<ChatResponseUpdate> ProcessUserMessageStream(AgentContext agentContext, AgentChatHistory agentChatHistory);
}
