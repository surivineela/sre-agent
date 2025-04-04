// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Extensions;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.Communication;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.SourceCodeAgent;

[DurableTask]
public class SourceCodePlanActivity : TaskActivity<SourceCodeAgentInput, List<Microsoft.Extensions.AI.ChatMessage>>
{
    private readonly IChatClient chatClient;
    private readonly SinkService sinkService;

    public SourceCodePlanActivity(IChatClient chatClient, SinkService sinkService)
    {
        this.chatClient = chatClient;
        this.sinkService = sinkService;
    }

    public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, SourceCodeAgentInput agentInput)
    {
        var input = agentInput.Input;
        var existingAppsDetails = string.Join(Environment.NewLine,
            input.AppsWithoutSourceCodeNodes.Select(x => $"{x.ResourceId} currently does not have a source code node."));

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "SourceCodeAgent", "SourceCodePlan.txt");
        var systemPrompt = File.ReadAllText(path);
        var userMessageText = $"Here are the apps that need updating: {existingAppsDetails}";

        var systemMessage = new ChatMessage(ChatRole.System, systemPrompt);
        var userMessage = new ChatMessage(ChatRole.User, userMessageText);
        List<ChatMessage> messages = [
            systemMessage,
            userMessage
            ];

        var threadContext = agentInput.Context;
        await sinkService.SinkSystemMessageAsync(threadContext, systemMessage.Text);

        var userThreadMessage = new ThreadMessage(
            ThreadId: threadContext.ThreadId, 
            MessageId: Guid.NewGuid(),
            Message: userMessage.Text,
            UserId: string.Empty,
            DisplayName: string.Empty,
            Timestamp: DateTime.UtcNow);
        await sinkService.SinkUserMessageAsync(threadContext, userThreadMessage, isVisibleInUserChatHistory: false);

        var response = await chatClient.GetResponseAsync(messages);
        messages.Add(response.GetMessage());

        return messages;
    }
}

