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

namespace Agent.Runtime.SubAgents.AppCodeAnalysisAgent
{
    [DurableTask]
    public class AppCodeAnalysisPlanActivity: TaskActivity<AppCodeAnalysisInput, List<Microsoft.Extensions.AI.ChatMessage>>
    {
        private readonly IChatClient chatClient;

        public AppCodeAnalysisPlanActivity(IChatClient chatClient)
        {
            this.chatClient = chatClient;
        }

        public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, AppCodeAnalysisInput input)
        {
            var existingAppsDetails = string.Join(Environment.NewLine,
                input.Apps.Select(x => $"The app {x.ResourceId} is down!"));
            var path = Path.Combine("..", "Agent.Runtime", "SubAgents", "AppCodeAnalysisAgent", "AppCodeAnalysisPlan.txt");
            var systemPrompt = File.ReadAllText(path);
            var userMessage = $"Here are the apps that need to be fixed: {existingAppsDetails}";
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
