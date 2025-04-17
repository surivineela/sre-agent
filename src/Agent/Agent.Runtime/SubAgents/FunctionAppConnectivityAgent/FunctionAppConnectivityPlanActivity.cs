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

namespace Agent.Runtime.SubAgents.FunctionAppConnectivityAgent
{
    [DurableTask]
    public class FunctionAppConnectivityPlanActivity: TaskActivity<FunctionAppConnectivityAgentInput, List<Microsoft.Extensions.AI.ChatMessage>>
    {
        private readonly IChatClient chatClient;
        public FunctionAppConnectivityPlanActivity(IChatClient chatClient)
        {
            this.chatClient = chatClient;
        }
        public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, FunctionAppConnectivityAgentInput input)
        {
            var existingAppsDetails = $@"Investigate the network connectivity of my function app: {input.FunctionAppResourceId}";

            var path = Path.Combine("..", "Agent.Runtime", "SubAgents", "FunctionAppConnectivityAgent", "FunctionAppConnectivityAgentPlan.txt");
            var systemPrompt = File.ReadAllText(path);

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, existingAppsDetails)
            ];
            var response = await chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());
            return messages;
        }
    } 
}
