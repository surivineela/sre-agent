using System;
using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.AI;
using Agent.Logging;
using Agent.Core.Models.Api.v1;
using Agent.Framework;

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
                    "- If the query pertains to yourself, reframe the question explicitly for Azure SRE Agent.\n" +
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
            // Call the core GenerateAsync that returns GeneratedEmbeddings so we can inspect Usage metadata.
            var generated = await embeddingGenerator.GenerateAsync(new[] { searchQuery }, options);

            if (generated == null || generated.Count == 0)
            {
                return Array.Empty<float>();
            }

            // Pull the first embedding result now so we can read its ModelId for logging
            var embedding = generated[0];

            // Log usage info when available, including model/modelVersion parsed from embedding.ModelId
            if (generated.Usage != null)
            {
                try
                {
                    var modelId = embedding?.ModelId?.ToString() ?? string.Empty;

                    // Split modelId into Model and ModelVersion when in format "model-modelVersion" (modelVersion is a date like 2025-04-14)
                    string model = modelId;
                    string modelVersion = string.Empty;

                    if (!string.IsNullOrEmpty(modelId))
                    {
                        var m = System.Text.RegularExpressions.Regex.Match(modelId, "^(.*)-(\\d{4}-\\d{2}-\\d{2})$");
                        if (m.Success && m.Groups.Count == 3)
                        {
                            model = m.Groups[1].Value;
                            modelVersion = m.Groups[2].Value;
                        }
                        else
                        {
                            // Fallback: put full modelId into model and leave modelVersion empty
                            model = modelId;
                            modelVersion = string.Empty;
                        }
                    }

                    // Use the project's structured logger helper for token consumption
                    logger.LogTokenConsumption(
                        model,
                        modelVersion,
                        generated.Usage.InputTokenCount ?? 0,
                        generated.Usage.OutputTokenCount ?? 0,
                        0, // CachedTokenCount not available on this Usage type
                        0); // ReasoningTokenCount not available on this Usage type
                }
                catch
                {
                    // don't fail the embedding flow because logging failed
                }
            }

            if (embedding == null)
            {
                return Array.Empty<float>();
            }

            return embedding.Vector.ToArray();
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error generating search vector");
            return Array.Empty<float>();
        }
    }

    public static async Task<List<string>> RerankWithLLM(
        IChatClient chatClient,
        string searchQuery,
        IReadOnlyList<SearchDocument> searchResults,
        ILogger logger)
    {
        try
        {
            var prompt = "You are a helpful assistant that ranks documents based on relevance to the search query.\n" +
                    "You will receive a search query and a list of documents. The documents are separated by '-' symbols.\n" +
                    "Rank the documents from most relevant to least relevant. Drop documents that are not relevant.\n" +
                    "Return an array of Id of each document in the ranked order. Do not include any prefix or suffix to each id.\n" +
                    "Search query: " + searchQuery + "\n" +
                    "Documents: " + string.Join("\n----------\n", searchResults.Select(d => $"<Id>{d.Id}</Id>\n<Content>{d.Content}</Content>"));

            var systemMessage = new ChatMessage(ChatRole.System, prompt);
            var chatOptions = new ChatOptions
            {
                ToolMode = ChatToolMode.None,
                Temperature = 0,
            };

            var (response, obj) = await chatClient.GetResponseAsync([systemMessage], typeof(List<string>), chatOptions);
            var result = obj as List<string> ?? new List<string>();
            return result;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Error reranking search results");
            return new List<string>();
        }
    }
}
