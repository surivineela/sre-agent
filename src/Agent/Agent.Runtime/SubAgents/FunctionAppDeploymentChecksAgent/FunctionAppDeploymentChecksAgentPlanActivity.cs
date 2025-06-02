using Agent.Core.Extensions;
using Microsoft.DurableTask;
using Microsoft.Extensions.AI;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.SubAgents.FunctionAppDeploymentChecksAgent
{
    /// <summary>
    /// Activity to generate the plan for the Function App Deployment Checks Agent
    /// </summary>
    [DurableTask]
    public class FunctionAppDeploymentChecksAgentPlanActivity(IChatClient chatClient) : TaskActivity<FunctionAppDeploymentChecksAgentInput, List<Microsoft.Extensions.AI.ChatMessage>>
    {
        private readonly IChatClient chatClient = chatClient;

        public async override Task<List<ChatMessage>> RunAsync(TaskActivityContext context, FunctionAppDeploymentChecksAgentInput input)
        {
            var existingAppsDetails = $@"Investigate and diagnose deployment issues with my function app: {input.FunctionAppResourceId}";
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "SubAgents", "FunctionAppDeploymentChecksAgent", "FunctionAppDeploymentChecksAgentPlan.txt");
            var systemPrompt = string.Empty;
            try
            {
                systemPrompt = await File.ReadAllTextAsync(path);
            }
            catch (Exception ex)
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
