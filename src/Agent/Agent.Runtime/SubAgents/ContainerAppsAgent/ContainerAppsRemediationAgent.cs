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
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "ContainerAppsAgent", "ContainerAppsAgent.txt");
        var systemPrompt = File.ReadAllText(path);
        var monitoringMessage = $"I was delegated to resolve container apps issue from another agent with message: {agentInput.Input}";

        List<ChatMessage> chatHistory = [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.System, monitoringMessage)
        ];

        // Run the generic reasoning loop to get actions and process function calls until the plan is complete.
        chatHistory = await RunReasoningLoopAsync(
            context,
            chatHistory,
            agentInput.ToolSignatures,
            agentInput.Context,
            log);

        return "success";
    }
}

