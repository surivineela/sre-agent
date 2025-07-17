using Agent.Core.Interfaces;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using System.Text;

namespace Agent.Runtime.SubAgents.CPUAnalysisAgent
{
    [DurableTask]
    public class CPUAnalysisAgent : GenericAgentOrchestrator<CPUAnalysisAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, CPUAnalysisAgentInput agentInput)
        {
            try
            {
                var log = context.CreateReplaySafeLogger<CPUAnalysisAgent>();

                // Initial planning phase: generate plan (e.g. list of apps to update)
                List<ChatMessage> chatHistory = await context.CallCPUAnalysisPlanActivityAsync(agentInput.Input);

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
