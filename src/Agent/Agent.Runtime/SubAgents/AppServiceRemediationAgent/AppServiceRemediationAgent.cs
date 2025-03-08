using Agent.Runtime.SubAgents.AppServiceRemediation;
using Agent.Runtime.SubAgents.Core;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents;

[DurableTask]
public class AppServiceRemediationAgent : GenericAgentOrchestrator<AppServiceRemediationAgentInput, string>
{
    private const string SystemPrompt = @"
You're a helpful agent to help diagnose issue and apply remediation of a Azure AppService resource.
You should first call all diagnose tools to find potential problems.
Then if any diagnosis implements the AppService resource is in unhealthy state, you should propose remediation to user accordingly.
If user approves your remediation plan, you go ahead apply the fix.";

    public override async Task<string> RunAsync(TaskOrchestrationContext context, AppServiceRemediationAgentInput agentInput)
    {
        List<ChatMessage> chatHistory = [
            new ChatMessage(ChatRole.System, SystemPrompt),
            new ChatMessage(ChatRole.User, $"My AppService resource id is: {agentInput.Input.AppServiceResourceId}")];

        // Run the generic reasoning loop to get actions and process function calls until the plan is complete.
        chatHistory = await RunReasoningLoopAsync(
            context,
            chatHistory,
            agentInput.ToolSignatures);

        return "success";
    }
}