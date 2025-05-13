// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;

// Follow the pattern of https://msazure.visualstudio.com/One/_git/AAPT-Antares-OperationalAgent?path=/docs/adding-a-sub-agent.md&_a=preview&version=GBmain
namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent
{
    public record RevisionAgentInput(
    ContainerAppRevisionAgentActivityInput Input,
    IReadOnlyList<string> ToolSignatures,
    Guid ThreadId)
    {
       
    }

    [DurableTask]
    public class ContainerAppRevisionAgent : GenericAgentOrchestrator<RevisionAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, RevisionAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<RevisionAgent.ContainerAppRevisionAgent>();
            // Initial planning phase: generate plan (e.g. list of apps to update)
            var chatHistory = await context.CallContainerAppRevisionAgentActivityAsync(agentInput.Input);

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

