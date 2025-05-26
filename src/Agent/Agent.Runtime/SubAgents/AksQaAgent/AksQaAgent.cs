using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Agent.Runtime.SubAgents.AksQaAgent; // Add this for AksQaAgentInput

namespace Agent.Runtime.SubAgents.AksQaAgent;

[DurableTask]
public class AksQaAgent : GenericAgentOrchestrator<AksQaAgentInput, string>
{
    public override async Task<string> RunAsync(TaskOrchestrationContext context, AksQaAgentInput agentInput)
    {
        var log = context.CreateReplaySafeLogger<AksQaAgentInput>();

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(SubAgents), nameof(AksQaAgent), "AksQaAgentPlan.txt");
        var systemPrompt = File.ReadAllText(path);
        var monitoringMessage = $"META AGENT REQUEST:\n {agentInput.Input}";

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
