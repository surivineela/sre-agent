// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;

namespace Agent.Runtime.AgentTasks;

public sealed class AgentTaskHandlerFactory(
    IReadOnlyDictionary<AgentTaskType, IAgentTaskHandler> agentTaskHandlers
)
{
    public IAgentTaskHandler GetHandler(AgentTaskType agentTaskType)
    {
        if (!agentTaskHandlers.TryGetValue(agentTaskType, out var handler))
        {
            throw new InvalidOperationException($"No handler found for agent task type: {agentTaskType}");
        }

        return handler;
    }
}
