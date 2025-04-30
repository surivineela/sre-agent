using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.RdpInvestigatorAgent;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.VmRdpInvestigatorAgent;

[DurableTask]
public class VmRdpInvestigatorAgent: GenericAgentOrchestrator<VmRdpInvestigatorAgentInput, string>
{
    public override async Task<string> RunAsync(TaskOrchestrationContext context, VmRdpInvestigatorAgentInput agentInput)
    {
        var log = context.CreateReplaySafeLogger<VmRdpInvestigatorAgent>();

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(SubAgents), nameof(VmRdpInvestigatorAgent), "VmRdpInvestigatorAgentPlan.txt");
        var systemPrompt = await File.ReadAllTextAsync(path);
        var userMessage = $"Please investigate RDP failure issue with VM {agentInput.VirtualMachineResourceId}";

        List<ChatMessage> chatHistory = [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, userMessage)
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
