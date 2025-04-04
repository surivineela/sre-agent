// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Extensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.AppServiceRemediation;

[DurableTask]
public class AppServiceRemediationPlanActivity : TaskActivity<AppServiceRemediationInput, List<ChatMessage>>
{
    private readonly IChatClient _chatClient;
    private readonly ILogger<AppServiceRemediationPlanActivity> _logger;

    public AppServiceRemediationPlanActivity(IChatClient chatClient, ILogger<AppServiceRemediationPlanActivity> logger)
    {
        _logger = logger;
        _chatClient = chatClient;
    }

    public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, AppServiceRemediationInput input)
    {
        _logger.LogInformation($"AppServiceRemediationPlanActivity started with input: {JsonSerializer.Serialize(input)}");
        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "AppServiceRemediationAgent", "AppServiceAgent.txt");
        var systemPrompt = await File.ReadAllTextAsync(path);

        List<ChatMessage> messages = [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.User, $"Here're the app service resource ids: {string.Join(", ", input.AppServiceResourceIds)}")
        ];

        _logger.LogInformation("AppServiceRemediationPlanActivity sending messages to chat client.");
        var response = await _chatClient.GetResponseAsync(messages);
        messages.Add(response.GetMessage());
        _logger.LogInformation($"AppServiceRemediationPlanActivity completed with response.");

        return messages;
    }
}

