// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.IncidentHandlerAgent;

public interface IIncidentHandlerAgent
{
    Task<string> ProcessIncidentAsync(AgentContext agentContext, AgentChatHistory agentChatHistory);

    IAsyncEnumerable<ChatResponseUpdate> ProcessIncidentStream(AgentContext agentContext, AgentChatHistory agentChatHistory);
}
