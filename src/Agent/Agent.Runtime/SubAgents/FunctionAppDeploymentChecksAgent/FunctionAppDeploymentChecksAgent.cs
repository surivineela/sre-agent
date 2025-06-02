using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Logging;

namespace Agent.Runtime.SubAgents.FunctionAppDeploymentChecksAgent;

/// <summary>
/// Agent for checking and analyzing Function App deployment information
/// </summary>
[DurableTask]
public class FunctionAppDeploymentChecksAgent : GenericAgentOrchestrator<FunctionAppDeploymentChecksAgentInput, string>
{
    /// <summary>
    /// Run the Function App Deployment Checks Agent
    /// </summary>
    public override async Task<string> RunAsync(TaskOrchestrationContext context, FunctionAppDeploymentChecksAgentInput agentInput)
    {
        var log = context.CreateReplaySafeLogger<FunctionAppDeploymentChecksAgent>();

        try
        {
            log.LogInternalInformation("Starting Function App Deployment Checks for {ResourceId}", agentInput.FunctionAppResourceId);

            // Initial planning phase: generate plan
            List<ChatMessage> chatHistory = await context.CallFunctionAppDeploymentChecksAgentPlanActivityAsync(agentInput);

            var monitoringMessage = $"Thank you for the confirmation, I will now check the deployment information of the Function App {agentInput.FunctionAppResourceId} for potential issues";

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
            log.LogInternalError(ex, "Error running Function App Deployment Checks Agent for {ResourceId}", agentInput.FunctionAppResourceId);
            return "failed";
        }
    }
}
