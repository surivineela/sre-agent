using Agent.Runtime.SubAgents.RdpInvestigatorAgent;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.VmRdpInvestigatorAgent;

[DurableTask]
public class VmRdpInvestigatorAgent: GenericAgentOrchestrator<VmRdpInvestigatorAgentInput, string>
{
    public override async Task<string> RunAsync(TaskOrchestrationContext context, VmRdpInvestigatorAgentInput agentInput)
    {
        var log = context.CreateReplaySafeLogger<VmRdpInvestigatorAgent>();
        try
        {
            // Initial planning phase: generate plan
            List<ChatMessage> chatHistory = await context.CallVmRdpInvestigatorPlanActivityAsync(agentInput);

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
        catch (Exception ex)
        {
            log.LogError(ex, "Error in VmRdpInvestigatorAgent");
            return $"Error: {ex.Message}";
        }
    }
}
