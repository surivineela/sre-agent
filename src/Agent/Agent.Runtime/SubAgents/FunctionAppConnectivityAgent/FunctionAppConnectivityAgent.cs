using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.FunctionAppConnectivityAgent
{
    [DurableTask]
    class FunctionAppConnectivityAgent : GenericAgentOrchestrator<FunctionAppConnectivityAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, FunctionAppConnectivityAgentInput agentInput)
        {
            var log = context.CreateReplaySafeLogger<FunctionAppConnectivityAgent>();

            // Initial planning phase: generate plan (e.g. list of apps to update)
            List<ChatMessage> chatHistory = await context.CallFunctionAppConnectivityPlanActivityAsync(agentInput);

            var monitoringMessage = $"Thank you for the confirmation, I will now attempt to investigate issues causing connectivity failure with {agentInput.FunctionAppResourceId}";

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
                agentInput.ThreadId);

            return "success";
        }
    }
}
