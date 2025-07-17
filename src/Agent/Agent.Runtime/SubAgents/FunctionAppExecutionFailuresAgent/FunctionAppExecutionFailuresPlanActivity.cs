using Agent.Core.Extensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using System.IO;

namespace Agent.Runtime.SubAgents.FunctionAppExecutionFailuresAgent
{
    [DurableTask]
    public class FunctionAppExecutionFailuresPlanActivity : TaskActivity<FunctionAppExecutionFailuresAgentInput, List<Microsoft.Extensions.AI.ChatMessage>>
    {
        private readonly IChatClient chatClient;
        
        public FunctionAppExecutionFailuresPlanActivity(IChatClient chatClient)
        {
            this.chatClient = chatClient;
        }
        
        public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, FunctionAppExecutionFailuresAgentInput input)
        {
            var functionAppDetails = $@"Investigate the execution failures of my function app: {input.FunctionAppResourceId}";
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "FunctionAppExecutionFailuresAgent", "FunctionAppExecutionFailuresAgentPlan.txt");
            var systemPrompt = string.Empty;
            try
            {
                systemPrompt = File.ReadAllText(path);
            }
            catch (Exception)
            {
                // Handle exception, e.g., log the error or set a default value for systemPrompt
                systemPrompt = "Default system prompt message.";
            }

            List<ChatMessage> messages = [
                new ChatMessage(ChatRole.System, systemPrompt),
                new ChatMessage(ChatRole.User, functionAppDetails)
            ];
            
            var response = await chatClient.GetResponseAsync(messages);
            messages.Add(response.GetMessage());
            return messages;
        }
    } 
}
