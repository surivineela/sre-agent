using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.RdpInvestigatorAgent;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.SqlDbQueryPerfAgent;

[DurableTask]
public class SqlDbQueryPerfAgent: GenericAgentOrchestrator<SqlDbQueryPerfAgentInput, string>
{
    public override async Task<string> RunAsync(TaskOrchestrationContext context, SqlDbQueryPerfAgentInput agentInput)
    {
        var log = context.CreateReplaySafeLogger<SqlDbQueryPerfAgentInput>();

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(SubAgents), nameof(SqlDbQueryPerfAgent), "SqlDbQueryPerfAgentPlan.txt");
        var systemPrompt = await File.ReadAllTextAsync(path);
        var monitoringMessage = $"I will now attempt to investigate query perf issues with {agentInput.AzSqlDbResourceId}. Once ll the investigation is complete, I will present my final findings in the following format"
            + "    **Summary**: Provide a brief overview of the investigation results."
            + "    **Identified Issues**: List any issues discovered during the investigation."
            + "    **Probable Root Cause**: Discuss the likely cause of the performance issues."
            + "    **Next Steps**: Outline the steps, in detail, to be taken for remediation, including immediate fixes and long-term strategies.";

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
            log,
            agentInput.ThreadId);

        return "success";
    }
}
