// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json.Serialization;
using Agent.Core.Models;
using Agent.Logging;
using Agent.Plugins.Clients;
using Agent.Plugins.Interface;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    [AgentToolPlugin(IsFirstPartyOnly = true, Category = ToolCategories.KnowledgeBase, ResourceType = ToolResourceTypes.ContainerApps)]
    public class AzureDocSearchPlugin : IAzureDocSearchPlugin
    {
        private const string INDEX_NAME = "rag-1747704319758";
        private record SearchChunk
        {
            [JsonPropertyName("chunk")]
            public string Chunk { get; set; }
        }
        private readonly IAzureSearchClient _searchClient;
        private readonly ILogger<AzureDocSearchPlugin> _logger;

        public AzureDocSearchPlugin(ILogger<AzureDocSearchPlugin> logger, IAzureSearchClient azureSearchClient)
        {
            _logger = logger;
            _searchClient = azureSearchClient;
        }

        [Description("Retrieves **internal design documents** that can aid understanding of architectural choices, component behaviors, or known mitigations.")]
        public async Task<string> SearchDesignDocsAsync(string query)
        {
            _logger.LogInternalInformation($"Vector search for internal design documents. Query: {query}");

            var searchResults = await _searchClient.SearchAsync<SearchChunk>(
                searchIndex: INDEX_NAME,
                searchText: query,
                configureOptions: options =>
                {
                    options.VectorSearch = new VectorSearchOptions
                    {
                        Queries =
                        {
                          new VectorizableTextQuery(query)
                                {
                                    Fields = { "text_vector" },
                                    KNearestNeighborsCount = 5
                                }
                        }
                    };

                    options.Select.Add("chunk");
                    options.Size = 5;
                });

            var topChunks = searchResults.GetResults()
                .Select(r => r.Document.Chunk)
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .ToList();

            if (topChunks.Count == 0)
            {
                _logger.LogInternalInformation("No vector results found.");
                return "No relevant content found in design documents.";
            }

            return string.Join("\n\n", topChunks);
        }
    }
}
