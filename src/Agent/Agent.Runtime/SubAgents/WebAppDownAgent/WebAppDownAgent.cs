using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.WebAppDownAgent
{
    [DurableTask]
    public class WebAppDownAgent : GenericAgentOrchestrator<WebAppDownAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, WebAppDownAgentInput agentInput)
        {
            try
            {
                var log = context.CreateReplaySafeLogger<WebAppDownAgent>();

                // Initial planning phase: generate plan (e.g. list of apps to update)
                List<ChatMessage> chatHistory = await context.CallWebAppDownPlanActivityAsync(agentInput.Input);

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
                    log,
                    agentInput.ThreadId,
                    agentInput.HelperAgentsInputs);

                return "success";
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
