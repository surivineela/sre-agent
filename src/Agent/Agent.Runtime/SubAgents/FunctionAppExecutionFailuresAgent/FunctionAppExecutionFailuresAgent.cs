using Agent.Runtime.SubAgents.Core;
using Agent.Logging;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.FunctionAppExecutionFailuresAgent
{
    [DurableTask]
    class FunctionAppExecutionFailuresAgent : GenericAgentOrchestrator<FunctionAppExecutionFailuresAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, FunctionAppExecutionFailuresAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<FunctionAppExecutionFailuresAgent>();

            try
            {
                // Initial planning phase: generate plan
                List<ChatMessage> chatHistory = await context.CallFunctionAppExecutionFailuresPlanActivityAsync(agentInput);

                var monitoringMessage = $"Thank you for the confirmation, I will now attempt to investigate execution failures with {agentInput.FunctionAppResourceId}";

                // Send a summary and start the execution
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
                    agentInput.ThreadId);
            }
            catch (Exception ex)
            {
                log.LogInternalError(ex, "An error occurred while running the FunctionAppExecutionFailuresAgent.");
                return "failure";
            }

            return "success";
        }
    }
}
