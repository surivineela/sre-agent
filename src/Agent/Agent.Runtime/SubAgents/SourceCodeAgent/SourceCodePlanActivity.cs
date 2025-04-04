// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Extensions;
using Agent.Core.Models;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.SourceCodeAgent;

[DurableTask]
public class SourceCodePlanActivity : TaskActivity<SourceCodeInput, List<Microsoft.Extensions.AI.ChatMessage>>
{
    private readonly IChatClient chatClient;

    public SourceCodePlanActivity(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, SourceCodeInput input)
    {
        var existingAppsDetails = string.Join(Environment.NewLine,
            input.AppsWithoutSourceCodeNodes.Select(x => $"{x.ResourceId} currently does not have a source code node."));

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "SourceCodeAgent", "SourceCodePlan.txt");
        var systemPrompt = File.ReadAllText(path);
        var userMessage = $"Here are the apps that need updating: {existingAppsDetails}";

        List<ChatMessage> messages = [
            new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userMessage)
            ];

        var response = await chatClient.GetResponseAsync(messages);
        messages.Add(response.GetMessage());

        return messages;
    }
}

