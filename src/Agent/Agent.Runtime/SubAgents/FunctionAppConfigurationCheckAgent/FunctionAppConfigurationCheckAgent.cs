using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Logging;

namespace Agent.Runtime.SubAgents.FunctionAppConfigurationCheck;

/// <summary>
/// Agent for checking and fixing Function App configuration issues
/// </summary>
[DurableTask]
public class FunctionAppConfigurationCheckAgent : GenericAgentOrchestrator<FunctionAppConfigurationCheckAgentInput, string>
{
    /// <summary>
    /// Run the Function App Configuration Check Agent
    /// </summary>
    public override async Task<string> RunAsync(TaskOrchestrationContext context, FunctionAppConfigurationCheckAgentInput agentInput)
    {
        var log = context.CreateReplaySafeLogger<FunctionAppConfigurationCheckAgent>();

        try
        {
            log.LogInternalInformation("Starting Function App Configuration Check for {ResourceId}", agentInput.FunctionAppResourceId);

            // Initial planning phase: generate plan
            List<ChatMessage> chatHistory = await context.CallFunctionAppConfigurationCheckAgentPlanActivityAsync(agentInput);

            var monitoringMessage = $"Thank you for the confirmation, I will now check the configuration of the Function App {agentInput.FunctionAppResourceId} for potential issues";

            // Send a summary and start the execution
            chatHistory = await context.CallSendSummaryAndStartActivityAsync(
                new GetNextActionInput
                {
                    ChatMessages = chatHistory,
                    StepCounter = 0,
                    ToolSignatures = agentInput.ToolSignatures
                });

            // Run the generic reasoning loop to get actions and process function calls until the plan is complete
            chatHistory = await RunReasoningLoopAsync(
                context,
                chatHistory,
                agentInput.ToolSignatures,
                log,
                agentInput.ThreadId);

            return $"completed for {agentInput.FunctionAppResourceId}";
        }
        catch (Exception ex)
        {
            log.LogInternalError(ex, "Error running Function App Configuration Check Agent for {ResourceId}", agentInput.FunctionAppResourceId);
            return "failed";
        }
    }
}
