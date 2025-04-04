// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Extensions;
using Agent.Core.Models;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.CVEAgent;

[DurableTask]
public class CVEPlanActivity : TaskActivity<CVEInput, List<Microsoft.Extensions.AI.ChatMessage>>
{
    private readonly IChatClient chatClient;

    public CVEPlanActivity(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, CVEInput input)
    {
        var existingAppsDetails = string.Join(Environment.NewLine,
            input.ReposToScan.Select(x => $"Going to scan {x.RepoUrl} for any security vulnerabilties."));

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "CVEAgent", "CVEPlan.txt");
        var systemPrompt = File.ReadAllText(path);

        List<ChatMessage> messages = [
            new ChatMessage(ChatRole.System, systemPrompt),
            ];

        var response = await chatClient.GetResponseAsync(messages);
        messages.Add(response.GetMessage());

        return messages;
    }
}

