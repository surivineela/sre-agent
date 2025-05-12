// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using Agent.Runtime.SubAgents.Core;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppsQuotaAgent
{
    // [MENDATORY]
    public record ContainerAppsQuotaAgentInput(
            ContainerAppsQuotaAgentActivityInput Input,
            IReadOnlyList<string> ToolSignatures,
            Guid ThreadId
        )       
    {
        
    }

    // [MENDATORY]
    [DurableTask]
    public class ContainerAppsQuotaAgent : GenericAgentOrchestrator<ContainerAppsQuotaAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, ContainerAppsQuotaAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<ContainerAppsQuotaAgent>();
            // Initial planning phase: generate plan (e.g. list of apps to update)
            var chatHistory = await context.CallContainerAppsQuotaAgentActivityAsync(agentInput.Input);

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
