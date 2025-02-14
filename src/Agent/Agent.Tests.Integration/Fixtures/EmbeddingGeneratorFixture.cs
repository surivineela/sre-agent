using Azure.AI.OpenAI;
using Azure.Identity;
using Microsoft.Extensions.AI;
using OpenAI.Chat;

using Agent.Core.Configuration;

namespace Agent.Tests.Integration.Fixtures
{
    /// <summary>
    /// 
    /// </summary>
    public class EmbeddingGeneratorFixture
    {
        internal IEmbeddingGenerator<string, Embedding<float>> EmbeddingGenerator { get; }

        public EmbeddingGeneratorFixture(AzureSettings azureSettings)
        {
            string aoaiEndpoint = azureSettings.OpenAI.Endpoint;
            string? key = azureSettings.OpenAI.ApiKey;
            string? deployment = azureSettings.OpenAI.EmbeddingGeneratorDeploymentName;

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
            EmbeddingGenerator = client.AsEmbeddingGenerator(deployment);
        }
    }
}