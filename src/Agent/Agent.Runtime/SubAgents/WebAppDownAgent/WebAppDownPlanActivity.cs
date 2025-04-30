using Agent.Core.Models;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Newtonsoft.Json;
using Agent.Core.Extensions;

namespace Agent.Runtime.SubAgents.WebAppDownAgent
{
    [DurableTask]
    public class WebAppDownPlanActivity: TaskActivity<string, List<Microsoft.Extensions.AI.ChatMessage>>
    {
        private readonly IChatClient chatClient;
        public WebAppDownPlanActivity(IChatClient chatClient)
        {
            this.chatClient = chatClient;
        }
        public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, string resourceId)
        {
            //var existingAppsDetails = string.Join(Environment.NewLine,
            //    input.Apps.Select(x => $"The app {x.ResourceId} is down!"));
            var existingAppDetails = $"The app {resourceId} is down!";
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, nameof(SubAgents), nameof(WebAppDownAgent), "WebAppDownPlan.txt");
            var systemPrompt = await File.ReadAllTextAsync(path);
            var userMessage = $"Here are the apps that need to be fixed: {existingAppDetails}";
            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, userMessage)
            ];
            var response = await chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());
            return messages;
        }
    }
}
