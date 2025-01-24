using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace OperationalAgentRuntime.Cli;

public static class DemoExec2
{
    private static readonly HttpClient _httpClient = new();

    public static async Task Execute(
        Kernel kernel,
        ILogger logger)
    {
        var chatCompletionService = kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();
        history.AddSystemMessage(Prompts.SystemMessage);

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

                Console.WriteLine("Assistant > " + result);

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
