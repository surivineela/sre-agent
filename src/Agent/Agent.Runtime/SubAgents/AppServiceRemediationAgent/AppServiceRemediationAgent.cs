using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.AppServiceRemediation;

[DurableTask]
public class AppServiceRemediationAgent : GenericAgentOrchestrator<AppServiceRemediationAgentInput, string>
{
    public override async Task<string> RunAsync(TaskOrchestrationContext context, AppServiceRemediationAgentInput agentInput)
    {
        var log = context.CreateReplaySafeLogger<AppServiceRemediationAgent>();
        // Initial planning phase: generate plan (e.g. list of apps to update)
        List<ChatMessage> chatHistory = await context.CallAppServiceRemediationPlanActivityAsync(agentInput.Input);

        // Optionally, send a summary and start the execution (this activity could be similar to your SendSummaryAndStartActivity)
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
            agentInput.ThreadId,
            log);

        return "success";
    }
}
