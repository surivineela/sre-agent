// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppIngressAgent
{
    public record ContainerAppIngressAgentInput(
        ContainerAppIngressAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
        )
    {
        
    }

    [DurableTask]
    public class ContainerAppIngressAgent : GenericAgentOrchestrator<ContainerAppIngressAgentInput, string>
    {
        public async override Task<string> RunAsync(TaskOrchestrationContext context, ContainerAppIngressAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<ContainerAppIngressAgent>();
            // Initial planning phase: generate plan (e.g. list of apps to update)
            var chatHistory = await context.CallContainerAppIngressAgentActivityAsync(agentInput.Input);

            // Run the generic reasoning loop to get actions and process function calls until the plan is complete.
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
