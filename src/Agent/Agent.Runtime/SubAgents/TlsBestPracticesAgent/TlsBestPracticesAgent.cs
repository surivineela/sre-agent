using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Plugins;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.TlsBestPractices
{
    [DurableTask]
    public class TlsBestPracticesAgent : GenericAgentOrchestrator<TlsBestPracticesAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, TlsBestPracticesAgentInput agentInput)
        {
            // Initial planning phase: generate plan (e.g. list of apps to update)
            List<ChatMessage> chatHistory = await context.CallTlsPlanActivityAsync(agentInput.Input);

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
                agentInput.ThreadId);

            return "success";
        }
    }
}
