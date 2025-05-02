using System.Text.Json;
using Agent.Core.Extensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.ContainerAppsRemediation;

[DurableTask]
public class ContainerAppsRemediationPlanActivity : TaskActivity<ContainerAppsRemediationAgentInput, List<ChatMessage>>
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<ContainerAppsRemediationPlanActivity> _logger;

    public ContainerAppsRemediationPlanActivity(IChatClient chatClient, ILogger<ContainerAppsRemediationPlanActivity> logger)
    {
        _logger = logger;
        _chatClient = chatClient;
    }

    public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerAppsRemediationAgentInput agentInput)
    {
        _logger.LogInformation($"ContainerAppsRemediationPlanActivity started with input: {JsonSerializer.Serialize(agentInput)}");
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "ContainerAppsAgent", "ContainerAppsAgent.txt");
        var systemPrompt = await File.ReadAllTextAsync(path);
        var monitoringMessage = $"I was delegated to resolve container apps issue from another agent with message: {agentInput.Input}";

        List<ChatMessage> chatHistory = [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.System, monitoringMessage)
        ];

        var response = await _chatClient.GetResponseAsync(chatHistory);
        chatHistory.Add(response.GetMessage());
        _logger.LogInformation($"ContainerAppsRemediationPlanActivity completed with response.");
        return chatHistory;
    }
}
