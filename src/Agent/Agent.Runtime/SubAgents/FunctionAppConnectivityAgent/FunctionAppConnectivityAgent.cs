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

            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(SubAgents), nameof(FunctionAppConnectivityAgent), "FunctionAppConnectivityAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            var monitoringMessage = $"Thank you for the confirmation, I will now attempt to investigate issues causing RDP failure with {agentInput.FunctionAppResourceId}";

            List<ChatMessage> chatHistory = [
                new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.System, monitoringMessage)
            ];

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
                agentInput.Context,
                log);

            return "success";
        }
    }
}
