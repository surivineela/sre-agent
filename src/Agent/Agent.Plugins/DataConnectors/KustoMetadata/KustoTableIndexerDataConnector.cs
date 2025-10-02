// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

namespace Agent.Plugins.DataConnectors.KustoMetadata
{
    using System.Collections.Generic;
    using System.Data;
    using System.Text;
    using System.Threading;
    using Agent.Core.Configuration;
    using Agent.Core.DataConnectors;
    using Agent.Core.Interfaces;
    using Azure.Storage.Blobs.Models;
    using Microsoft.Extensions.AI;
    using Microsoft.Extensions.Logging;
    using Polly;
    using Polly.Retry;

    [DataConnector("KustoDataIndexer")]
    public class KustoTableIndexerDataConnector : IDataConnector
    {
        private const string TableRootPath = "tables";
        private const string ExampleQueriesRootPath = "examples";
        private const int MaxUploadRetries = 3;

        // Maximum age of a Kusto table metadata document before it is considered for re-indexing
        private static readonly TimeSpan MaxDocumentAge = TimeSpan.FromHours(48);

        // Interval to wait between column content generation.
        // Column content generation calls the LLM for each column in a table, which can cause throttling if done too quickly.
        private static readonly TimeSpan ColumnInterval = TimeSpan.FromSeconds(5);

        // Uploading the final doc to blob storage is the last step.
        // If it fails, a lot of work goes to waste.
        private readonly AsyncRetryPolicy _blobUploadRetryPolicy;
        private readonly ILogger<KustoTableIndexerDataConnector> _logger;
        private readonly IChatClient _chatClient;
        private readonly ILoggerFactory _loggerFactory;
        private readonly DataConnectorIndex _kustoMetadataIndex;
        private readonly DataConnectorStorage<KustoTableIndexerDataConnector> _storage;
        private readonly IAuthenticationService _authService;

        private DataConnectorInstanceSettings? _dataConnectorInstanceSettings;
        private KustoTableSummarizer? _kustoSummarizer;

        public KustoTableIndexerDataConnector(
            IChatClient chatClient,
            ILoggerFactory loggerFactory,
            DataConnectorIndex kustoMetadataIndex,
            DataConnectorStorage<KustoTableIndexerDataConnector> storage,
            IAuthenticationService authService)
        {
            _loggerFactory = loggerFactory ?? throw new ArgumentNullException(nameof(loggerFactory));
            _logger = loggerFactory.CreateLogger<KustoTableIndexerDataConnector>();
            _chatClient = chatClient ?? throw new ArgumentNullException(nameof(chatClient));
            _kustoMetadataIndex = kustoMetadataIndex;
            _storage = storage;
            _authService = authService;

            _blobUploadRetryPolicy = Policy
                .Handle<Exception>()
                .WaitAndRetryAsync(
                    MaxUploadRetries,
                    retryAttempt => TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) / 2),
                    (exception, timeSpan, retryCount, context) =>
                    {
                        _logger.LogInternalWarning(exception, "Failed to upload blob to storage. Attempt {Attempt}. Retrying in {Delay}s.", retryCount, timeSpan.TotalSeconds);
                    });
        }

        public TimeSpan Interval
        {
            get
            {
                return TimeSpan.FromHours(12);
            }
        }

        public Task InitAsync(DataConnectorInstanceSettings instanceSettings, CancellationToken stoppingToken)
        {
            _dataConnectorInstanceSettings = instanceSettings ?? throw new ArgumentNullException(nameof(instanceSettings));

            _logger.LogInternalInformation($"Using managed identity resource ID {instanceSettings.Identity} and auth source {instanceSettings.Source} for Kusto summarizer.");

            _kustoSummarizer = new KustoTableSummarizer(_chatClient, new Uri(instanceSettings.DataSource), instanceSettings.Identity, instanceSettings.Source, _loggerFactory, _authService);

            return Task.CompletedTask;
        }

        public async Task RunAsync(CancellationToken stoppingToken)
        {
            Uri clusterUri = new Uri(_dataConnectorInstanceSettings!.DataSource);

            _logger.LogInternalInformation("Processing cluster: {ClusterUri}", clusterUri);

            try
            {
                IEnumerable<string> databases = await DiscoverDatabasesAsync(clusterUri, stoppingToken);

                foreach (string database in databases)
                {
                    stoppingToken.ThrowIfCancellationRequested();

                    try
                    {
                        IEnumerable<string> tableNames = await DiscoverTablesAsync(database, stoppingToken);

                        foreach (string table in tableNames)
                        {
                            stoppingToken.ThrowIfCancellationRequested();

                            try
                            {
                                try
                                {
                                    _logger.LogInternalInformation("Checking last modified time for blob: {ClusterUri}, {DatabaseName}, {TableName}.", clusterUri, database, table);

                                    DateTimeOffset lastModified = await GetKustoMetadataBlobLastModifiedAsync(clusterUri.ToString(), database, table, stoppingToken);

                                    if (DateTimeOffset.UtcNow - lastModified < MaxDocumentAge)
                                    {
                                        _logger.LogInternalInformation("Skipping table {TableName} in database {DatabaseName} as it is not older than {MaxDocumentAge}. Last modified time: {LastModified}", table, database, MaxDocumentAge, lastModified);
                                        continue;
                                    }
                                }
                                catch (Azure.RequestFailedException ex) when (ex.Status == 404)
                                {
                                    // Blob does not exist, we can proceed
                                    _logger.LogInternalInformation("Blob for {ClusterUri}, {DatabaseName}, {TableName} does not exist.", clusterUri, database, table);
                                }

                                _logger.LogInternalInformation("Getting table schema, time stamp column, and message columns for {DatabaseName}, {TableName}", database, table);

                                IReadOnlyList<KeyValuePair<string, string>> columnSchema = await _kustoSummarizer!.GetTableSchemaAsync(database, table, stoppingToken);
                                string timeStampColumn = await _kustoSummarizer.DiscoverTimeStampColumnsAsync(database, table, columnSchema, stoppingToken);

                                if (string.IsNullOrEmpty(timeStampColumn))
                                {
                                    throw new InvalidOperationException($"No timestamp column found for {database}, {table}.");
                                }

                                IEnumerable<string> logMessageColumnNames = await _kustoSummarizer.DiscoverLogMessageColumnsAsync(database, table, columnSchema.Select(x => x.Key), timeStampColumn, stoppingToken);
                                List<KustoColumnMetadata> columnMetaData = new List<KustoColumnMetadata>(columnSchema.Count);

                                foreach (KeyValuePair<string, string> column in columnSchema)
                                {
                                    stoppingToken.ThrowIfCancellationRequested();

                                    try
                                    {
                                        _logger.LogInternalInformation("Getting description for column {ColumnName} for {DatabaseName}, {TableName}", column.Key, database, table);

                                        string description = await _kustoSummarizer!.CreateColumnDescriptionAsync(
                                                database,
                                                table,
                                                column.Key,
                                                timeStampColumn,
                                                logMessageColumnNames,
                                                stoppingToken);

                                        if (!string.IsNullOrEmpty(description))
                                        {
                                            KustoColumnMetadata updatedColumn = new KustoColumnMetadata()
                                            {
                                                Name = column.Key,
                                                Type = column.Value,
                                                Description = description
                                            };

                                            _logger.LogInternalDebug("Column: {ColumnName}, Type: {ColumnType}, Description: {ColumnDescription}", updatedColumn.Name, updatedColumn.Type, updatedColumn.Description);

                                            columnMetaData.Add(updatedColumn);
                                        }
                                        else
                                        {
                                            _logger.LogInternalInformation("Column: {ColumnName} did not have enough data to form a description. Skipping it.", column.Key);
                                        }
                                    }
                                    catch (Exception ex) when (ex.IsNotTokenCancellation(stoppingToken))
                                    {
                                        _logger.LogInternalWarning(ex, "Failed to get description for column {ColumnName} in table {TableName} in database {DatabaseName}. Skipping it.", column.Key, table, database);
                                    }

                                    await Task.Delay(ColumnInterval, stoppingToken);
                                }

                                IReadOnlyList<KustoLogMessageSamples> logMessageSamples = await GetLogMessageSamplesAsync(database, table, timeStampColumn, logMessageColumnNames, stoppingToken);

                                string tableSummary = await _kustoSummarizer!.SummarizeTableAsync(table, logMessageSamples, columnMetaData, stoppingToken);

                                _logger.LogInternalInformation("Getting example queries and description for {DatabaseName}", database);

                                IEnumerable<KustoExampleQueryAndDescription> exampleQueries = await GetExampleQueriesForTableAsync(database, table, tableSummary, stoppingToken);

                                _logger.LogInternalInformation("Uploading table summary and example queries and description for {DatabaseName}, {TableName}", database, table);

                                await UploadJsonDocumentationAsync(clusterUri, database, table, tableSummary, columnMetaData, logMessageSamples, exampleQueries);

                            }
                            catch (Exception ex) when (ex.IsNotTokenCancellation(stoppingToken))
                            {
                                _logger.LogInternalWarning(ex, "Failed to process table {TableName} in database {DatabaseName}. Skipping it.", table, database);
                            }
                        }
                    }
                    catch (Exception ex) when (ex.IsNotTokenCancellation(stoppingToken))
                    {
                        _logger.LogInternalWarning(ex, "Failed to process database {DatabaseName} in cluster {ClusterUri}. Skipping it.", database, clusterUri);
                    }
                }

                // Start the indexer after processing all databases and tables
                await _kustoMetadataIndex.RunIndexerAsync();
            }
            // Replace the catch block at the end of RunAsync with the following:

            catch (Exception ex) when (ex.IsNotTokenCancellation(stoppingToken))
            {
                _logger.LogInternalError(ex, "Failed to process cluster {ClusterUri}.", clusterUri);
            }
        }

        private async Task<IEnumerable<string>> DiscoverDatabasesAsync(Uri clusterUri, CancellationToken cancellationToken)
        {
            _logger.LogInternalInformation("Getting all databases for cluster {ClusterUri}", clusterUri);

            const string query = ".show databases";

            using IDataReader databasesData = await _kustoSummarizer!.PerformQueryAsync(string.Empty, query, cancellationToken);

            List<string> databaseNames = new List<string>();
            while (databasesData.Read())
            {
                string? databaseName = databasesData["DatabaseName"].ToString();
                if (!string.IsNullOrEmpty(databaseName))
                {
                    databaseNames.Add(databaseName);
                }
            }

            return databaseNames;
        }

        private async Task<IEnumerable<string>> DiscoverTablesAsync(string databaseName, CancellationToken cancellationToken)
        {
            _logger.LogInternalInformation("Getting all tables for database {DatabaseName}", databaseName);

            const string query = ".show tables";

            using IDataReader tablesData = await _kustoSummarizer!.PerformQueryAsync(databaseName, query, cancellationToken);

            List<string> tableNames = new List<string>();
            while (tablesData.Read())
            {
                string? tableName = tablesData["TableName"].ToString();
                if (!string.IsNullOrEmpty(tableName))
                {
                    tableNames.Add(tableName);
                }
            }

            return tableNames;
        }

        private async Task<IReadOnlyList<KustoLogMessageSamples>> GetLogMessageSamplesAsync(string database, string table, string timestampColumnName, IEnumerable<string> logMessageColumnNames, CancellationToken cancellationToken)
        {
            _logger.LogInternalInformation("Getting log message samples for {DatabaseName}, {TableName}", database, table);

            List<KustoLogMessageSamples> logMessageSamples = new List<KustoLogMessageSamples>(logMessageColumnNames.Count());
            foreach (string logMessageColumn in logMessageColumnNames)
            {
                string logData = await _kustoSummarizer!.GetLogMessageSamplesAsync(database, table, logMessageColumn, timestampColumnName, cancellationToken);

                if (!string.IsNullOrEmpty(logData))
                {
                    logMessageSamples.Add(new KustoLogMessageSamples
                    {
                        LogColumnName = logMessageColumn,
                        UniqueMessages = logData.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                                .Distinct(StringComparer.OrdinalIgnoreCase)
                                                .ToList()
                    });
                }
            }
            return logMessageSamples;
        }

        private async Task<DateTimeOffset> GetKustoMetadataBlobLastModifiedAsync(string clusterUrl, string databaseName, string tableName, CancellationToken cancellationToken)
        {
            BlobProperties properties = await _storage.GetBlobPropertiesAsync(GetBlobNameForKustoTableMetadata(clusterUrl, databaseName, tableName), cancellationToken);

            return properties.LastModified;
        }

        private Task UploadKustoMetadataToBlob(KustoTableMetadata data)
        {
            return UploadJsonToBlob(GetBlobNameForKustoTableMetadata(data.ClusterUri, data.DatabaseName, data.TableName), data);
        }

        private Task UploadKustoExampleQueriesToBlob(KustoExampleQueryDocument data)
        {
            return UploadJsonToBlob(GetBlobNameForKustoExampleQuery(data), data);
        }

        private static string GetSafeKustoClusterName(Uri clusterUri)
        {
            return clusterUri.Host
                .ToLowerInvariant()
                .Replace(".kusto.windows.net", string.Empty)
                .Replace(".", "-");
        }

        private async Task UploadJsonToBlob(string blobName, DataConnectorSourceDocument data)
        {
            await _blobUploadRetryPolicy.ExecuteAsync(async () =>
            {
                await _storage.UploadBlobContentsAsync(blobName, data);
            });
        }

        private static string GetBlobNameForKustoTableMetadata(string clusterUrl, string databaseName, string tableName)
        {
            return $"{TableRootPath}/{GetSafeKustoClusterName(new Uri(clusterUrl))}_{databaseName}/{tableName}.json";
        }

        private static string GetBlobNameForKustoExampleQuery(KustoExampleQueryDocument exampleQuery)
        {
            return $"{ExampleQueriesRootPath}/{GetSafeKustoClusterName(new Uri(exampleQuery.ClusterUri))}_{exampleQuery.DatabaseName}/{exampleQuery.TableName}_example_queries.json";
        }

        private async Task UploadJsonDocumentationAsync(
           Uri clusterUri,
           string databaseName,
           string tableName,
           string tableDescription,
           IEnumerable<KustoColumnMetadata> columns,
           IEnumerable<KustoLogMessageSamples> logMessageSamples,
           IEnumerable<KustoExampleQueryAndDescription> exampleQueries)
        {

            string clusterName = GetSafeKustoClusterName(clusterUri);

            _logger.LogInternalInformation("Generating JSON documentation for {Cluster}_{DatabaseName}_{TableName}", clusterName, databaseName, tableName);

            StringBuilder sb = new StringBuilder();

            sb.AppendLine("# Kusto Table");
            sb.AppendLine($"Name: {tableName}");
            sb.AppendLine($"Description: {tableDescription}");
            sb.AppendLine();
            sb.AppendLine("## Columns");
            sb.AppendLine("| Name | Type | Description |");
            sb.AppendLine("|------|------|-------------|");
            sb.AppendLine();

            foreach (KustoColumnMetadata column in columns)
            {
                sb.AppendLine($"| {column.Name} | {column.Type} | {column.Description} |");
            }

            if (logMessageSamples.Any())
            {
                sb.AppendLine("## Log message unique samples");
                sb.AppendLine();

                foreach (KustoLogMessageSamples sample in logMessageSamples)
                {
                    if (sample.UniqueMessages.Count > 0)
                    {
                        sb.AppendLine($"### {sample.LogColumnName}");
                        sb.AppendLine();
                        sb.AppendLine(string.Join(Environment.NewLine, sample.UniqueMessages));
                        sb.AppendLine();
                    }
                }
            }

            KustoTableMetadata tableMetadata = new KustoTableMetadata
            {
                Id = $"{clusterName}_{databaseName}_{tableName}", //id cannot have a ".", causes indexer to fail index the data from blob
                ClusterUri = clusterUri.ToString(),
                DatabaseName = databaseName,
                TableName = tableName,
                Title = tableName,
                TableDescription = tableDescription,
                LogMessageSamples = new List<KustoLogMessageSamples>(logMessageSamples),
                Columns = new List<KustoColumnMetadata>(columns),
                Contents = sb.ToString(),
                Filter = "Table"
            };

            await UploadKustoMetadataToBlob(tableMetadata);

            var exampleQueryDoc = new KustoExampleQueryDocument
            {
                Id = $"{clusterName}_{databaseName}_{tableName}_examplequeries",
                ClusterUri = clusterUri.ToString(),
                DatabaseName = databaseName,
                TableName = tableName,
                Title = tableName,
                ExampleQueries = exampleQueries.Any()
                    ? exampleQueries.ToList()
                    : new List<KustoExampleQueryAndDescription>
                    {
                        new()
                        {
                            Id = $"{databaseName}_{tableName}_none",
                            Description = "No example queries available.",
                            Query = ""
                        }
                    },
                Contents = $"{tableDescription} {string.Join(" ", exampleQueries.Select(q => $"{q.Description} {q.Query}"))}",
                Filter = "Example"
            };

            await UploadKustoExampleQueriesToBlob(exampleQueryDoc);
        }

        private async Task<IEnumerable<KustoExampleQueryAndDescription>> GetExampleQueriesForTableAsync(string databaseName, string tableName, string tableDescription, CancellationToken cancellationToken)
        {
            var baseDir = AppContext.BaseDirectory;
            var queriesFolder = Path.Combine(baseDir, "Plugins", "Definitions", "Queries");

            if (!Directory.Exists(queriesFolder))
            {
                _logger.LogInternalWarning("Queries folder does not exist: {QueriesFolder}", queriesFolder);
                throw new Exception("Folder dont exist");
            }

            var kqlFiles = Directory.GetFiles(queriesFolder, "*.kql", SearchOption.AllDirectories);
            var result = new List<KustoExampleQueryAndDescription>();

            foreach (var file in kqlFiles)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var lines = File.ReadAllLines(file);
                var content = string.Join(Environment.NewLine, lines);

                if (!content.Contains(tableName, StringComparison.OrdinalIgnoreCase))
                    continue;

                string description;
                string queryText = content;

                if (lines.Length > 0 && lines[0].TrimStart().StartsWith("// Description:", StringComparison.OrdinalIgnoreCase))
                {
                    description = lines[0].Substring("// Description:".Length).Trim();
                    queryText = string.Join(Environment.NewLine, lines.Skip(1));
                }
                else
                {
                    // Generate description using AI if not present
                    description = await _kustoSummarizer!.GenerateQueryDescriptionAsync(tableDescription, queryText, cancellationToken);
                }
                // Use a deterministic Id, e.g., databaseName_tableName_{i} or a hash of the query
                string id = $"{databaseName}_{tableName}_{Path.GetFileNameWithoutExtension(file)}";
                result.Add(new KustoExampleQueryAndDescription { Id = id, Description = description, Query = queryText });
            }

            return result;
        }
    }
}
