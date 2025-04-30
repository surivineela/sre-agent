// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Extensions;
using Agent.Core.Models;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.ManagedIdentityMigration;

[DurableTask]
public class ManagedIdentityPlanActivity : TaskActivity<ManagedIdentityMigrationInput, List<ChatMessage>>
{
    private readonly IChatClient chatClient;

    public ManagedIdentityPlanActivity(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, ManagedIdentityMigrationInput input)
    {
        var existingAppsDetails = string.Join(Environment.NewLine,
            input.AppsToMigrate.Select(x => $"{x.ResourceId} ({x.Name}) currently uses {x.CurrentConnectionMethod}"));

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "ManagedIdentityMigrationAgent", "ManagedIdentityMigrationPlan.txt");
        var systemPrompt = await File.ReadAllTextAsync(path);
        var monitoringMessage = $"A monitoring service found that these apps that need migration to Managed Identity: {existingAppsDetails}";

        List<ChatMessage> messages = [
            new ChatMessage(ChatRole.System, systemPrompt),
            new ChatMessage(ChatRole.System, monitoringMessage)
        ];

        var response = await chatClient.GetResponseAsync(messages);
        messages.Add(response.GetMessage());

        return messages;
    }
}

