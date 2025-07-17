using Agent.Core.Extensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.FunctionAppConfigurationCheck
{
    /// <summary>
    /// Activity to generate the plan for the Function App Configuration Check Agent
    /// </summary>
    [DurableTask]
    public class FunctionAppConfigurationCheckAgentPlanActivity(IChatClient chatClient) : TaskActivity<FunctionAppConfigurationCheckAgentInput, List<Microsoft.Extensions.AI.ChatMessage>>
    {
        private readonly IChatClient chatClient = chatClient;

        public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, FunctionAppConfigurationCheckAgentInput input)
        {
            var existingAppsDetails = $@"Investigate and diagnose configuration issues with my function app: {input.FunctionAppResourceId}";
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "FunctionAppConfigurationCheckAgent", "FunctionAppConfigurationCheckAgentPlan.txt");
            var systemPrompt = string.Empty;
            try
            {
                systemPrompt = await File.ReadAllTextAsync(path);
            }
            catch (Exception)
            {
                // Handle exception, e.g., log the error or set a default value for systemPrompt
                systemPrompt = "Default system prompt message.";
            }
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
