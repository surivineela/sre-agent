using Agent.Core.Extensions;
using Agent.Runtime.SubAgents.RdpInvestigatorAgent;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents.VmRdpInvestigatorAgent;

[DurableTask]
public class VmRdpInvestigatorPlanActivity: TaskActivity<VmRdpInvestigatorAgentInput, List<Microsoft.Extensions.AI.ChatMessage>>
{
    private readonly IChatClient chatClient;

    public VmRdpInvestigatorPlanActivity(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, VmRdpInvestigatorAgentInput input)
    {
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(SubAgents), nameof(VmRdpInvestigatorAgent), "VmRdpInvestigatorAgentPlan.txt");
        var userMessage = $@"I was delegated to resolve RDP failure issue from another agent for VM: {input.VirtualMachineResourceId}";
        var systemPrompt = string.Empty;
        try
        {
            systemPrompt = await File.ReadAllTextAsync(path);
        }
        catch (Exception ex)
        {
            // Handle exception, e.g., log the error or set a default value for systemPrompt
            systemPrompt = "Default system prompt message.";
        }

        List<ChatMessage> messages = [
            new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userMessage)
        ];

        var response = await chatClient.GetResponseAsync(messages);
        messages.Add(response.GetMessage());
        return messages;
    }
}
