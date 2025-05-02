// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents.ContainerAppsRemediation;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents;

[DurableTask]
public class ContainerAppsRemediationAgent : GenericAgentOrchestrator<ContainerAppsRemediationAgentInput, string>
{
    public override async Task<string> RunAsync(TaskOrchestrationContext context, ContainerAppsRemediationAgentInput agentInput)
    {
        var log = context.CreateReplaySafeLogger<ContainerAppsRemediationAgent>();

        List<ChatMessage> chatHistory = await context.CallContainerAppsRemediationPlanActivityAsync(agentInput);

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

