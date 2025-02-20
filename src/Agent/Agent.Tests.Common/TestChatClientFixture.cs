using Azure.AI.OpenAI;
using Azure.Identity;
using OpenAI.Chat;

using Agent.Core.Configuration;
using Microsoft.Extensions.AI;

namespace Agent.Tests.Common.Fixtures
{
    /// <summary>
    /// 
    /// </summary>
    public class TestChatClientFixture
    {
        public IChatClient ChatClient { get; }

        public TestChatClientFixture(AzureSettings azureSettings)
        {
            string aoaiEndpoint = azureSettings.OpenAI.Endpoint;
            string? key = azureSettings.OpenAI.ApiKey;
            string? deployment = azureSettings.OpenAI.DeploymentName;

            // Validate required settings
            if (string.IsNullOrEmpty(aoaiEndpoint))
            {
                throw new InvalidOperationException("The `AzureOpenAIEndpoint` setting is required. Check the README for more information.");
            }

            if (string.IsNullOrEmpty(deployment))
            {
                throw new InvalidOperationException("The `AzureOpenAIDeployment` setting is required. Check the README for more information.");
            }

            Console.WriteLine($" * Using Azure OpenAI endpoint: {aoaiEndpoint}");

            // Create the Azure OpenAI client
            AzureOpenAIClient client;
            if (string.IsNullOrEmpty(key))
            {
                Console.WriteLine("No `OpenAIAPI_KEY` found. Using DefaultAzureCredential.");
                client = new AzureOpenAIClient(new Uri(aoaiEndpoint), new DefaultAzureCredential());
            }
            else
            {
                client = new AzureOpenAIClient(new Uri(aoaiEndpoint), new System.ClientModel.ApiKeyCredential(key));
            }

            // Return the ChatClient instance
            ChatClient = client.AsChatClient(deployment);
        }

        public async Task<bool> MatchesNaturalLanguagePrompt(string expected, IList<Microsoft.Extensions.AI.ChatMessage> chatHistory)
        {
            IList<Microsoft.Extensions.AI.ChatMessage> tmpChatHistory = chatHistory.Where(m => m.Role == ChatRole.Assistant || m.Role == ChatRole.Tool).ToList();

            tmpChatHistory.Add(new(ChatRole.User, $@"You are part of an end to end unit testing framework.
Your job is simply to respond with `true` or `false` depending on if the logs from this chat history match the expected text.
The text doesn't have to match exactly, but it needs to be close enough that a human would say it's an acceptible response for what we're trying to accomplish.

Expected: {expected}"
            ));
            ChatResponse completion = await ChatClient.GetResponseAsync(tmpChatHistory);

            bool succeeded = bool.TryParse(completion.Message.Text, out var result);
            if (!succeeded)
            {
                throw new Exception($"Natural language test failed to parse the result. Response was: {completion}");
            }
            return result;
        }
    }
}