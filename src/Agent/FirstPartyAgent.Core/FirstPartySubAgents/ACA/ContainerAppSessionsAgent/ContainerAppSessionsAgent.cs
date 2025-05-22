// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;

// Follow the pattern of https://msazure.visualstudio.com/One/_git/AAPT-Antares-OperationalAgent?path=/docs/adding-a-sub-agent.md&_a=preview&version=GBmain
namespace FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppSessionsAgent
{
    public record SessionsAgentInput(
        ContainerAppSessionsAgentActivityInput Input,
        IReadOnlyList<string> ToolSignatures,
        Guid ThreadId
        )
    {
    }

    [DurableTask]
    public class ContainerAppSessionsAgent : GenericAgentOrchestrator<SessionsAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, SessionsAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<ContainerAppSessionsAgent>();
            // Initial planning phase: generate plan (e.g. list of apps to update)
            var chatHistory = await context.CallContainerAppSessionsAgentActivityAsync(agentInput.Input);

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

