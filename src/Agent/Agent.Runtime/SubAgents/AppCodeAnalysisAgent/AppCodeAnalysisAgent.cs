using Agent.Core.Interfaces;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Azure.ResourceManager.AppService.Models;

namespace Agent.Runtime.SubAgents.AppCodeAnalysisAgent
{
    [DurableTask]
    public class AppCodeAnalysisAgent : GenericAgentOrchestrator<AppCodeAnalysisAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, AppCodeAnalysisAgentInput agentInput)
        {
            try
            {
                var log = context.CreateReplaySafeLogger<AppCodeAnalysisAgent>();

                // Initial planning phase: generate plan (e.g. list of apps to update)
                List<ChatMessage> chatHistory = await context.CallAppCodeAnalysisPlanActivityAsync(agentInput.Input);


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
                    agentInput.ThreadId);

                return "success";
            }
            catch (Exception)
            {
                throw;
            }
        }
    }
}
