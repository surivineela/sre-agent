// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Data;
using System.Text;
using Agent.Core.Clients.Search;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Core.Models.Search;
using Agent.Framework;
using Agent.Plugins.DataConnectors.KustoMetadata;
using Agent.Plugins.Kusto;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Kusto.Data.Exceptions;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(Category = ToolCategories.LogQuery)]
    public class LogsPluginDefinition : ContextToolTarget<AgentContext>
    {
        private const int MaxTables = 5;

        private static readonly TimeSpan MaxQueryTime = TimeSpan.FromSeconds(90);

        private readonly KustoMetadataIndex<KustoTableMetadata> _kustoMetadataIndex;
        private readonly KustoClient _kustoClient;
        private readonly ISearchIndexingClient _searchClient;
        private readonly ILogger<LogsPluginDefinition> _logger;

        public LogsPluginDefinition(
            ISearchIndexingClient searchClient,
            KustoMetadataIndex<KustoTableMetadata> kustoMetadataIndex,
            KustoClient kustoClient,
            ILogger<LogsPluginDefinition> logger
            )
        {
            _kustoMetadataIndex = kustoMetadataIndex ?? throw new ArgumentNullException(nameof(kustoMetadataIndex));
            _searchClient = searchClient ?? throw new ArgumentNullException(nameof(searchClient));
            _kustoClient = kustoClient ?? throw new ArgumentNullException(nameof(kustoClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        [Description("This tool gets information about kusto tables that are relevant to a user's chat message. The information includes table names, their column schema, and sample log messages.")]
        public async Task<string> GetKustoTableMetadataAsync(string chatMessage, CancellationToken cancellationToken = default)
        {
            _logger.LogInternalInformation("Searching index {Index} with query: {Query}", _kustoMetadataIndex.IndexName, chatMessage);

            IAsyncEnumerable<SearchResult<KustoTableMetadata>> tables = await SearchKustoTableMetadataAsync(chatMessage, _kustoMetadataIndex.IndexName, MaxTables);

            return await PreprocessAsync(tables);
        }

        [Description("This tool validates a Kusto query against the specified cluster and database. It returns the number of rows returned by the query or an error message if the query is invalid.")]
        public async Task<string> ValidateQueryAsync(string clusterUri, string database, string query, CancellationToken cancellationToken = default)
        {
            _logger.LogInternalInformation("Validating query: Cluster: {ClusterUri}, Database: {Database}, Query: {Query}", clusterUri, database, query);

            if (string.IsNullOrWhiteSpace(query))
            {
                return "Query cannot be empty.";
            }

            using CancellationTokenSource cancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cancellationTokenSource.CancelAfter(MaxQueryTime);

            try
            {
                using IDataReader result = await _kustoClient.PerformQueryAsync(clusterUri, database, query, cancellationTokenSource.Token);

                int rowCount = 0;
                while (result.Read())
                {
                    ++rowCount;
                }

                return $"The query was executed successfully and returned {rowCount} rows.";
            }
            catch (OperationCanceledException ex) when (ex.CancellationToken == cancellationTokenSource.Token)
            {
                return $"The query execution took too long and was cancelled after {MaxQueryTime.TotalSeconds} seconds.";
            }
            catch (KustoRequestException ex)
            {
                return $"The query you provided is invalid and failed due to the following reason: {ex.Message}.";
            }
            catch (Exception ex)
            {
                return $"Failed to execute the query you provided due to the following reason: {ex.Message}.";
            }
        }

        private async Task<string> PreprocessAsync(IAsyncEnumerable<SearchResult<KustoTableMetadata>> tables)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("# Kusto Table Information");

            await foreach (SearchResult<KustoTableMetadata> result in tables)
            {
                KustoTableMetadata table = result.Document;

                _logger.LogInternalInformation("Processing search results. Table: {Table}. Search score: {Score}", table.TableName, result.Score);

                sb.AppendLine($"## Cluster URI `{table.ClusterUri}`");
                sb.AppendLine($"## Database `{table.DatabaseName}`");
                sb.AppendLine($"## Table `{table.TableName}`");
                sb.AppendLine("### Description");
                sb.AppendLine(table.TableDescription);
                sb.AppendLine("### Columns");
                sb.AppendLine("| Name | Type | Description |");
                sb.AppendLine("|------|------|-------------|");

                foreach (KustoColumnMetadata column in table.Columns)
                {
                    sb.AppendLine($"| {column.Name} | {column.Type} | {column.Description} |");
                }

                sb.AppendLine("### Log Message Columns and Sample Data");

                foreach (KustoLogMessageSamples sample in table.LogMessageSamples)
                {
                    sb.AppendLine($"#### Column: `{sample.LogColumnName}`");
                    sb.AppendLine("Sample Messages:");
                    if (sample.UniqueMessages.Count > 0)
                    {
                        foreach (string message in sample.UniqueMessages) 
                        {
                            sb.AppendLine($"{message}");
                        }
                    }
                    else
                    {
                        sb.AppendLine("No sample messages available.");
                    }
                }
            }

            return sb.ToString();
        }

        private async Task<IAsyncEnumerable<SearchResult<KustoTableMetadata>>> SearchKustoTableMetadataAsync(string query, string indexName, int max)
        {
            SearchOptions searchOptions = new SearchOptions()
            {
                QueryType = SearchQueryType.Simple,
                Size = max,
                VectorSearch = new VectorSearchOptions()
                {
                    Queries =
                    {
                        new VectorizableTextQuery(query)
                        {
                            Fields = { _kustoMetadataIndex.VectorFieldName },
                            KNearestNeighborsCount = max,
                        }
                    }
                }
            };

            if (_kustoMetadataIndex.SemanticSearchTitleField != null)
            {
                SemanticSearchOptions semanticSearchOptions = new SemanticSearchOptions();

                foreach (SearchField field in _kustoMetadataIndex.SemanticSearchContentFields)
                {
                    semanticSearchOptions.SemanticFields.Add(field.Name);
                }

                searchOptions.SemanticSearch = semanticSearchOptions;
            }

            SearchResults<KustoTableMetadata> searchResults = await _searchClient.SearchAsync<KustoTableMetadata>(
                indexName: indexName,
                searchText: query,
                searchOptions: searchOptions);

            return searchResults.GetResultsAsync();
        }
    }
}
