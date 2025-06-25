// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.KubernetesAgent;

[DurableTask]
public class KubernetesAgent : GenericAgentOrchestrator<KubernetesAgentInput, string>
{
    public override async Task<string> RunAsync(TaskOrchestrationContext context, KubernetesAgentInput agentInput)
    {
        var log = context.CreateReplaySafeLogger<KubernetesAgent>();

        try
        {
            // Initial planning phase: generate plan
            List<ChatMessage> chatHistory = await context.CallKubernetesAgentPlanActivityAsync(agentInput);

            // Send a summary and start the execution (this activity could be similar to your SendSummaryAndStartActivity)
            chatHistory = await context.CallSendSummaryAndStartActivityAsync(
                     new GetNextActionInput
                     {
                         ChatMessages = chatHistory,
                         StepCounter = 0,
                         ToolSignatures = [],
                     });

            // Run the generic reasoning loop to get actions and process function calls until the plan is complete.
            chatHistory = await RunReasoningLoopAsync(
                context,
                chatHistory,
                agentInput.ToolSignatures,
                log,
                agentInput.ThreadId,
                agentInput.HelperAgentsInputs);

            return "success";
        }
        catch (Exception ex)
        {
            log.LogInternalError(ex, "Error in KubernetesAgent: {Message}", ex.Message);
            return $"Error: {ex.Message}";
        }
    }
}
