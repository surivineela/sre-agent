using System.Text.Json;
using Agent.Core.Extensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents.ContainerImagePullFailureAgent;

[DurableTask]
public class ContainerImagePullFailurePlanActivity : TaskActivity<ContainerImagePullFailureInput, List<ChatMessage>>
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<ContainerImagePullFailurePlanActivity> _logger;

    public ContainerImagePullFailurePlanActivity(IChatClient chatClient, ILogger<ContainerImagePullFailurePlanActivity> logger)
    {
        _logger = logger;
        _chatClient = chatClient;
    }

    public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ContainerImagePullFailureInput input)
    {
        _logger.LogInformation($"ContainerImagePullFailurePlanActivity started with input: {JsonSerializer.Serialize(input)}");
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "ContainerImagePullFailureAgent", "ContainerImagePullFailureAgent.txt");
        var systemPrompt = await File.ReadAllTextAsync(path);

        List<ChatMessage> messages = [
             new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User,
                $"I need to investigate an image pull failure for the following Linux/Container App: {input.resourceId}")
         ];

        _logger.LogInformation("ContainerImagePullFailurePlanActivity sending messages to chat client.");
        var response = await _chatClient.GetResponseAsync(messages);
        messages.Add(response.GetMessage());
        _logger.LogInformation($"ContainerImagePullFailurePlanActivity completed with response.");

        return messages;
    }
}
