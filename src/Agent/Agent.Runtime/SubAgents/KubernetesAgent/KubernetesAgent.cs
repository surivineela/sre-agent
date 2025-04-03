// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.KubernetesAgent;

[DurableTask]
public class KubernetesAgent : GenericAgentOrchestrator<KubernetesAgentInput, string>
{
    public override async Task<string> RunAsync(TaskOrchestrationContext context, KubernetesAgentInput agentInput)
    {
        var log = context.CreateReplaySafeLogger<KubernetesAgent>();
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "KubernetesAgent", "KubernetesAgent.txt");
        var systemPrompt = File.ReadAllText(path);
        var monitoringMessage = $"You have been asked to resolve Kubernetes workloads issue from another agent with message: {agentInput.Input}";

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

