// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Clients.Search;
using Agent.Core.Models.Search;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Indexing.KustoQueryGeneration
{
    /// <summary>
    /// Experimental class for generating Kusto queries based on user intent and table metadata.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal class KustoQueryGenerator
    {        
        private readonly IChatClient _chatClient;
        private readonly ChatOptions _chatOptions;
        private const string INDEX_NAME = "kustotables";
        private const int MAX_TABLES_TO_FETCH = 3; // Limit to top 3 tables for simplicity
        private readonly ISearchIndexingClient _searchClient;

        public KustoQueryGenerator(            
            IChatClient chatClient,
            ISearchIndexingClient searchClient
            )
        {            
            _chatClient = chatClient;
            _searchClient = searchClient;
            _chatOptions  = new ChatOptions
            {
                Temperature = 0.5f,                
            };
        }

        public async Task<string> GenerateKustoQueryAsync(string userQuery, CancellationToken cancellationToken = default)
        {
            // 1. Search Kusto table metadata using the plugin interface
            List<KustoTableMetadata> tables = await SearchKustoTableMetadataAsync(userQuery);

            // 2. Preprocess results (e.g., take top 3, extract fields)
            var topTables = tables.Take(MAX_TABLES_TO_FETCH).ToList();

            // 3. Prepare input for LLM
            string llmInput = PreprocessForLLM(topTables, userQuery);
            // 4. Call LLM/chat client to generate Kusto query
            string prompt = $"Given the following user query and relevant Kusto table metadata, generate a Kusto query that best answers the user's request.\n\n{llmInput}\n\nKusto Query:";
            var messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, "You are an expert in generating Kusto queries based on user intent and table metadata."),
                new ChatMessage(ChatRole.User, prompt)
            };

            ChatResponse response = await _chatClient.GetResponseAsync(messages, _chatOptions, cancellationToken);
            string kustoQuery = response.Messages.Last().Text;

            return kustoQuery;
        }


        // we can do further preprocessing here splitting inputs by appname, ids, env names and sending them to LLM for faster processing and generation
        private string PreprocessForLLM(IEnumerable<KustoTableMetadata> tables, string userQuery)
        {
            var tableSummaries = tables.Select(t =>
    $"Table: {t.TableName}, Columns: {string.Join(", ", t.Columns.Select(c => $"{c.Name} ({c.Type})"))}, Description: {t.TableDescription}"
            );
            return $"User Query: {userQuery}\n\nRelevant Tables:\n{string.Join("\n", tableSummaries)}";
        }

        private async Task<List<KustoTableMetadata>> SearchKustoTableMetadataAsync(string query)
        {
            var searchResults = await _searchClient.SearchAsync<KustoTableMetadata>(
                indexName: INDEX_NAME,
                searchText: query,
                searchOptions: new Azure.Search.Documents.SearchOptions() { QueryType = Azure.Search.Documents.Models.SearchQueryType.Semantic, Size = MAX_TABLES_TO_FETCH });

            var topResults = searchResults.GetResults()
                .Select(r => r.Document)
                .ToList();

            return topResults;
        }
    }
}
