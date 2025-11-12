// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Data;
using System.Text;
using Agent.Core.DataConnectors;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins.DataConnectors.KustoMetadata;
using Agent.Plugins.Kusto;
using Kusto.Data.Exceptions;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(Category = ToolCategories.LogQuery)]
    public class LogsPluginDefinition : ContextToolTarget<AgentContext>
    {
        private const int MaxTables = 2;

        private static readonly TimeSpan MaxQueryTime = TimeSpan.FromSeconds(90);

        private readonly DataConnectorIndex _dataConnectorIndex;
        private readonly KustoClient _kustoClient;
        private readonly ILogger<LogsPluginDefinition> _logger;

        public LogsPluginDefinition(
            DataConnectorIndex kustoMetadataIndex,
            KustoClient kustoClient,
            ILogger<LogsPluginDefinition> logger
            )
        {
            _kustoClient = kustoClient ?? throw new ArgumentNullException(nameof(kustoClient));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _dataConnectorIndex = kustoMetadataIndex;
        }

        [Description("This tool gets information about kusto tables that are relevant to a user's chat message. The information includes table names, their column schema, and sample log messages.")]
        public async Task<string> GetKustoTableMetadataAsync(string chatMessage, CancellationToken cancellationToken = default)
        {
            _logger.LogInternalInformation("Searching index with query: {Query}", chatMessage);

            IAsyncEnumerable<DataConnectorSearchResult<KustoTableMetadata>> tables = _dataConnectorIndex.SearchAsync<KustoTableMetadata>(chatMessage, string.Empty, MaxTables);

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

        private async Task<string> PreprocessAsync(IAsyncEnumerable<DataConnectorSearchResult<KustoTableMetadata>> tables)
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine("# Kusto Table Information");

            int count = 0;
            await foreach (DataConnectorSearchResult<KustoTableMetadata> result in tables)
            {
                ++count;

                KustoTableMetadata table = result.OriginalDocument;

                _logger.LogInternalInformation("Processing search results. Table: {Table}. Search score: {Score}", table.TableName, result.SearchResult.Score);

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

            _logger.LogInternalInformation("Found {Count} relevant tables in index", count);

            if (count == 0)
            {
                sb.AppendLine("No relevant Kusto tables found. This tool relies on Kusto table metadata that is generated by the KustoDataIndexer Data Connector. Please ensure you have a valid KustoDataIndexer Data Connector.");
            }

            return sb.ToString();
        }
    }
}
