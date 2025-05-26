// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvironmentAgent
{
    public record ContainerAppEnvironmentAgentInput(
            ContainerAppEnvironmentAgentActivityInput Input,
            IReadOnlyList<string> ToolSignatures,
            Guid ThreadId
        )
    {

    }

    [DurableTask]
    public class ContainerAppEnvironmentAgent : GenericAgentOrchestrator<ContainerAppEnvironmentAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, ContainerAppEnvironmentAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<ContainerAppEnvironmentAgent>();
            // Initial planning phase: generate plan (e.g. list of apps to update)
            var chatHistory = await context.CallContainerAppEnvironmentAgentActivityAsync(agentInput.Input);

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
