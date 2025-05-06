using System.Text.Json;
using Agent.Core.Extensions;
using Agent.Logging;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.KubernetesAgent;

[DurableTask]
public class KubernetesAgentPlanActivity : TaskActivity<KubernetesAgentInput, List<ChatMessage>>
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<KubernetesAgentPlanActivity> _logger;

    public KubernetesAgentPlanActivity(IChatClient chatClient, ILogger<KubernetesAgentPlanActivity> logger)
    {
        _logger = logger;
        _chatClient = chatClient;
    }

    public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, KubernetesAgentInput agentInput)
    {
        _logger.LogInternalInformation($"KubernetesAgentPlanActivity started with input: {JsonSerializer.Serialize(agentInput)}");
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "KubernetesAgent", "KubernetesAgent.txt");
        var systemPrompt = await File.ReadAllTextAsync(path);
        var monitoringMessage = $"META AGENT REQUEST:\n {agentInput.Input}";

        List<ChatMessage> chatHistory = [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.System, monitoringMessage)
        ];

        var response = await _chatClient.GetResponseAsync(chatHistory);
        chatHistory.Add(response.GetMessage());
        _logger.LogInternalInformation($"KubernetesAgentPlanActivity completed with response.");
        return chatHistory;
    }
}
