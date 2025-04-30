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

namespace Agent.Runtime.SubAgents.AppReliabilityAgent
{
    [DurableTask]
    public class ReliabilityPlanActivity: TaskActivity<AppReliabilityInput, List<Microsoft.Extensions.AI.ChatMessage>>
    {
        private readonly IChatClient chatClient;
        public ReliabilityPlanActivity(IChatClient chatClient)
        {
            this.chatClient = chatClient;
        }
        public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, AppReliabilityInput input)
        {
            var existingAppsDetails = string.Join(Environment.NewLine,
                input.AppsInViolation.Select(x => $"{x.ResourceId} has a current reliability of {JsonConvert.SerializeObject(new Tuple<int, bool, bool, bool>(x.NumberOfWorkers, x.AlwaysOnEnabled, x.AutoHealEnabled, x.HealthCheckEnabled ))}"));
            var path = Path.Combine("..", "Agent.Runtime", "SubAgents", "AppReliabilityAgent", "AppReliabilityPlan.txt");
            var systemPrompt = (await File.ReadAllTextAsync(path)).Replace("{{desiredReliability}}", JsonConvert.SerializeObject(new Tuple<int, bool, bool, bool>(3, true, true, true)));
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
}
