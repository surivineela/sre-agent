using System;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Agent.Logging;

namespace Agent.Core.Helpers;

public static class DocumentRetrieval
{
    public static async Task<string> GenerateSearchQuery(IChatClient chatClient, IList<ChatMessage> chatHistory, string userMessage, ILogger logger)
    {
        try
        {
            var prompt = "Generate a search query based on the user's new question and the conversation history, ensuring it: \n" +
                    "- Concisely captures the user's intent or problem.\n" +
                    "- Avoids including entity names, IDs, source filenames, or document names.\n" +
                    "- Excludes any text inside brackets ([]) or special characters.\n" +
                    "- Translates non-English questions into English before framing the search query.\n" +
                    "\n\n" +
                    "The chat history is:\n";
            foreach (var msg in chatHistory)
            {
                prompt += $"{msg.Role}: {msg.Text}\n";
            }
            prompt += $"user: Generate search query for: {userMessage}\n";
            var systemMessage = new ChatMessage(ChatRole.System, prompt);
            var chatOptions = new ChatOptions
            {
                ToolMode = ChatToolMode.None,
                Temperature = 0,
            };

            var response = await chatClient.GetResponseAsync(systemMessage, chatOptions);
            return response.Text;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error generating search query");
            return string.Empty;
        }
    }

    public static async Task<float[]> GenerateSearchVector(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, string searchQuery, int dimensions, ILogger logger)
    {
        try
        {
            var options = new EmbeddingGenerationOptions
            {
                Dimensions = dimensions,
            };
            var embedding = await embeddingGenerator.GenerateAsync(searchQuery, options);
            return embedding.Vector.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error generating search vector");
            return Array.Empty<float>();
        }
    }
}
