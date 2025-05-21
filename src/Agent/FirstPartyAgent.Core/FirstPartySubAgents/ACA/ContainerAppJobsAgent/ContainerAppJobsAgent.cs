// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppJobsAgent
{
    [DurableTask]
    public class ContainerAppJobsAgent : GenericAgentOrchestrator<ContainerAppJobsAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, ContainerAppJobsAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<ContainerAppJobsAgent>();
            
            var chatHistory = await context.CallContainerAppJobsAgentActivityAsync(agentInput.Input);

            chatHistory = await RunReasoningLoopAsync(
                context,
                chatHistory,
                agentInput.ToolSignatures,
                log,
                agentInput.ThreadId);

            return "success";
        }
    }
}
