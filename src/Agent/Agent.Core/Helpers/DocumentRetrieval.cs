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
            var prompt = "Below is a history of the conversation so far, and a new question asked by the user that needs to be answered by searching in a knowledge base.\n" +
                    "You have access to Azure AI Search index with 100's of documents.\n" +
                    "Generate a search query based on the conversation and the new question.\n" +
                    "Do not include cited source filenames and document names e.g. info.txt or doc.pdf in the search query terms.\n" +
                    "Do not include any text inside [] or <<>> in the search query terms.\n" +
                    "Do not include any special characters like '+' in the search query terms.\n" +
                    "If the question is not in English, translate the question to English before generating the search query.\n" +
                    "\n\n" +
                    "The chat hisotry is:\n";
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

    public static async Task<float[]> GenerateSearchVector(IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator, string searchQuery, ILogger logger)
    {
        try
        {
            var embedding = await embeddingGenerator.GenerateAsync(searchQuery);
            return embedding.Vector.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error generating search vector");
            return Array.Empty<float>();
        }
    }
}
