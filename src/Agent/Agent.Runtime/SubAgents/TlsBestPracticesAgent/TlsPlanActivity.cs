using Agent.Core.Models;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.TlsBestPractices;

[DurableTask]
public class TlsPlanActivity : TaskActivity<TlsBestPracticesInput, List<Microsoft.Extensions.AI.ChatMessage>>
{
    private readonly IChatClient chatClient;

    public TlsPlanActivity(IChatClient chatClient)
    {
        this.chatClient = chatClient;
    }

    public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, TlsBestPracticesInput input)
    {
        var existingAppsDetails = string.Join(Environment.NewLine,
            input.AppsInViolation.Select(x => $"{x.ResourceId} has a current minimum TLS version of {x.MinimumTlsVersion}"));

        var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "TlsBestPracticesAgent", "TlsBestPracticesPlan.txt");
        var systemPrompt = File.ReadAllText(path).Replace("{{desiredVersion}}", input.DesiredVersion);
        var userMessage = $"Here are the apps that need updating: {existingAppsDetails}";

        List<ChatMessage> messages = [
            new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userMessage)
            ];

        var response = await chatClient.GetResponseAsync(messages);
        messages.Add(response.Message);

        return messages;
    }
}
