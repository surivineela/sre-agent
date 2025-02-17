using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using OpenAI.Chat;
using OperationalAgentCore.Models;

namespace OperationalAgentCore;

public static class DemoExec2
{
    private static readonly HttpClient _httpClient = new();

    public static async Task Execute(
        Kernel kernel,
        IConfiguration configuration,
        ILogger logger)
    {
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();

        string agentModeStr = configuration["AgentMode"] ?? string.Empty;
        var agentMode = Enum.TryParse<AgentMode>(agentModeStr, out var mode) ? mode : AgentMode.SREAgent;

        string systemPrompt = agentMode == AgentMode.ICM ? ICMAgent.SystemMessage : IssueFinderAgent.SystemMessage;

        var history = new ChatHistory();
        history.AddSystemMessage(systemPrompt);

        int? lastTokenCount = null;
        while (true)
        {

            // Send HTTP POST request
            try
            {
                // Get the response from the AI
                var result = await chatCompletionService.GetChatMessageContentAsync(
                    history,
                    executionSettings: new()
                    {
                        FunctionChoiceBehavior = FunctionChoiceBehavior.Auto()
                    },
                    kernel: kernel);

                // get the totaltokencount out of ChatTokenUsage in result.Metadasta array
                int? totalTokenCount = result.Metadata?.TryGetValue("Usage", out var chatTokenUsage) == true ? (chatTokenUsage as ChatTokenUsage)?.TotalTokenCount : null;

                Console.WriteLine($"Assistant ({totalTokenCount} tokens, +{totalTokenCount - (lastTokenCount ?? 0)}) > " + result);

                if (result.Metadata?.TryGetValue("tool_calls", out var toolCalls) == true &&
                    toolCalls.ToString().Contains("end_conversation"))
                {
                    Console.WriteLine("Assistant > I'm going to end the conversation.");
                    continue;
                }

                await TeamsNotificationHelper.SendTeamsNotificationAsync(
                   _httpClient,
                    result.Content);

                history.AddMessage(result.Role, result.Content ?? string.Empty);
                lastTokenCount = totalTokenCount;
            }
            catch (HttpOperationException ex)
            {
                Console.WriteLine($"Assistant > Azure Open AI Error occurred while sending the message: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Assistant > Error occurred while sending the message: {ex.Message}");
            }

            // Add the message from the agent to the chat history
            Console.Write("User > ");
            var userInput = Console.ReadLine();
            if (string.IsNullOrEmpty(userInput))
            {
                return;
            }

            history.AddUserMessage(userInput);
        }
    }
}
