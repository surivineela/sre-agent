// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Runtime.AgentTasks.Handlers;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Runtime.AgentTasks;

public sealed class AgentTaskHandlerFactory(
    IServiceProvider serviceProvider
)
{
    public IAgentTaskHandler GetHandler(AgentTaskType agentTaskType)
    {
        return agentTaskType switch
        {
            AgentTaskType.IncidentInvestigation => serviceProvider.GetRequiredService<IncidentInvestigationTaskHandler>(),
            _ => throw new InvalidOperationException($"No handler found for agent task type: {agentTaskType}")
        };
    }
}
