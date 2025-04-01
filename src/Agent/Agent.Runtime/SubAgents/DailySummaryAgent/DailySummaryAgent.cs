using Agent.Plugins.Definitions;
using Agent.Plugins;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Agent.Runtime.SubAgents.DailyReportSummary
{
    [DurableTask]
    public class DailyReportSummaryAgent : GenericAgentOrchestrator<DailyReportSummaryAgentInput, string>
    {
        public override async Task<string> RunAsync(TaskOrchestrationContext context, DailyReportSummaryAgentInput agentInput)
        {
            // Initial planning phase: generate the report plan (no visible output to user)
            List<ChatMessage> chatHistory = await context.CallDailyReportPlanActivityAsync(agentInput.Input);
            var log = context.CreateReplaySafeLogger<DailyReportSummaryAgent>();

            // We don't want to send a summary, just start execution silently
            // Only the final report will be shown to the user
            // Modified to skip the summary step
            chatHistory.Add(new ChatMessage(ChatRole.Assistant, "Starting report generation. Will provide complete report when finished."));

            // Add a specific instruction to avoid intermediate updates
            //chatHistory.Add(new ChatMessage(ChatRole.System, "IMPORTANT: Do not send intermediate status updates during report generation. Only provide the final comprehensive report at the end."));
            chatHistory.Add(new ChatMessage(ChatRole.System, @"IMPORTANT AGENT INSTRUCTIONS:
1. Do NOT use the MarkPlanComplete function at any point during this process.
2. Do not send intermediate status updates during report generation.
3. Continue analyzing and collecting data until you have a comprehensive report.
4. When you're done, simply use NotifyUser with the complete report.
5. This is critical: Under NO circumstances should you call MarkPlanComplete until explicitly instructed by the user."));


            // Run the generic reasoning loop to get actions and process function calls until the plan is complete
            // This will collect all the data silently without intermediate updates
            chatHistory = await RunReasoningLoopAsync(
                context,
                chatHistory,
                agentInput.ToolSignatures,
                agentInput.Context,
                log);

            // Once complete, ensure the final message is sent to the thread
            /*
            await context.CallNotifyUserActivityAsync(
                new NotifyUserInput
                {
                    ThreadId = agentInput.ThreadId,
                    Message = "Report generation complete. Please see the comprehensive summary above."
                });
            */
            return "success";
        }
    }
}
