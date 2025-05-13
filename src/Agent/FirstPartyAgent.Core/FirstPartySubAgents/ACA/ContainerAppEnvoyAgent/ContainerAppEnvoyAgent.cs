// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;

namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvoyAgent
{
    public record ContainerAppEnvoyAgentInput(
        ContainerAppEnvoyAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
        )
    {
        
    }

    [DurableTask]
    public class ContainerAppEnvoyAgent : GenericAgentOrchestrator<ContainerAppEnvoyAgentInput, string>
    {
        public async override Task<string> RunAsync(TaskOrchestrationContext context, ContainerAppEnvoyAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<ContainerAppEnvoyAgent>();
            // Initial planning phase: generate plan (e.g. list of apps to update)
            var chatHistory = await context.CallContainerAppEnvoyAgentActivityAsync(agentInput.Input);

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
