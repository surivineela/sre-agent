// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.V2;

public interface ISubAgentV2
{
    Task DoWork(
        AgentChatHistory? agentChatHistory,
        bool initWithSystemPrompt = false);
}
