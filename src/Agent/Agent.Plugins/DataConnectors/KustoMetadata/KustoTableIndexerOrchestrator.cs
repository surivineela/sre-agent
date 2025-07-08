// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

namespace Agent.Plugins.DataConnectors.KustoMetadata
{
    using System.Data;
    using System.Linq;
    using System.Text;
    using Agent.Core.Clients.Storage;
    using Agent.Core.Configuration;
    using Agent.Core.Interfaces;
    using Agent.Core.Models.Search;
    using Azure.Core;
    using Microsoft.DurableTask;
    using Microsoft.Extensions.Logging;
    using Microsoft.Extensions.Options;

    public record KustoConnectionInfo(Uri ClusterUri, string ManagedIdentityClientId, IEnumerable<string> DatabaseFilter, IEnumerable<string> TableFilter);

    public record KustoDatabaseInput(string DatabaseName);

    public record ExampleQuery(string Description, string QueryText);

    public record KustoTableIndexInput(string DatabaseName, string TableName);

    public record KustoTableIndexColumnMetadata(IEnumerable<string> LogMessageColumnNames, IEnumerable<KustoColumnMetadata> ColumnMetadata);

    public record KustoTableIndexColumnDescriptionInput(string DatabaseName, string TableName, string ColumnName, IEnumerable<string> ContextColumnNames);

    public record KustoTableIndexSummaryInput(Uri ClusterUri, string DatabaseName, string TableName, KustoTableIndexColumnMetadata Columns);

    [DurableTask]
    public class KustoTableIndexerOrchestrator : TaskOrchestrator<KustoConnectionInfo, bool>
    {
        public override async Task<bool> RunAsync(TaskOrchestrationContext context, KustoConnectionInfo clusterDetails)
        {
            ILogger logger = context.CreateReplaySafeLogger<KustoTableIndexerOrchestrator>();

            try
            {
                IEnumerable<string> databases = await context.CallKustoDatabaseDiscoveryActivityAsync(clusterDetails);

                if (clusterDetails.DatabaseFilter != null && clusterDetails.DatabaseFilter.Any())
                {
                    databases = databases.Where(db => clusterDetails.DatabaseFilter.Contains(db, StringComparer.OrdinalIgnoreCase));
                }

                foreach (string database in databases)
                {
                    try
                    {
                        KustoDatabaseInput databaseDetails = new KustoDatabaseInput(database);

                        IEnumerable<string> tableNames = await context.CallKustoTableIndexTableDiscoveryActivityAsync(databaseDetails);

                        if (clusterDetails.TableFilter != null && clusterDetails.TableFilter.Any())
                        {
                            tableNames = tableNames.Where(table => clusterDetails.TableFilter.Contains(table, StringComparer.OrdinalIgnoreCase));
                        }

                        foreach (string table in tableNames)
                        {
                            try
                            {
                                KustoTableIndexInput tableIndexInput = new KustoTableIndexInput(databaseDetails.DatabaseName, table);

                                KustoTableIndexColumnMetadata columnMetadata = await context.CallKustoTableIndexTableSchemaActivityAsync(tableIndexInput);

                                List<KustoColumnMetadata> updatedColumnMetaData = new List<KustoColumnMetadata>(columnMetadata.ColumnMetadata.Count());

                                foreach (KustoColumnMetadata column in columnMetadata.ColumnMetadata)
                                {
                                    try
                                    {
                                        string description = await context.CallKustoTableIndexColumnDescriptionActivityAsync(
                                            new KustoTableIndexColumnDescriptionInput(
                                                tableIndexInput.DatabaseName,
                                                tableIndexInput.TableName,
                                                column.Name,
                                                columnMetadata.LogMessageColumnNames));

                                        if (!string.IsNullOrEmpty(description))
                                        {
                                            KustoColumnMetadata updatedColumn = new KustoColumnMetadata()
                                            {
                                                Name = column.Name,
                                                Type = column.Type,
                                                Description = description
                                            };

                                            logger.LogInternalDebug("Column: {ColumnName}, Type: {ColumnType}, Description: {ColumnDescription}", updatedColumn.Name, updatedColumn.Type, updatedColumn.Description);

                                            updatedColumnMetaData.Add(updatedColumn);
                                        }
                                        else
                                        {
                                            logger.LogInternalInformation("Column: {ColumnName} did not have enough data to form a description. Skipping it.", column.Name);
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        logger.LogInternalError(ex, "Failed to get description for column {ColumnName} in table {TableName} in database {DatabaseName}. Skipping it.", column.Name, table, tableIndexInput.DatabaseName);
                                    }
                                }

                                KustoTableIndexColumnMetadata updatedMetaData = new KustoTableIndexColumnMetadata(columnMetadata.LogMessageColumnNames, updatedColumnMetaData);

                                await context.CallKustoTableIndexSummarizeAndUploadActivityAsync(new KustoTableIndexSummaryInput(clusterDetails.ClusterUri, tableIndexInput.DatabaseName, tableIndexInput.TableName, updatedMetaData));

                            }
                            catch (Exception ex)
                            {
                                logger.LogInternalError(ex, "Failed to get schema for table {TableName} in database {DatabaseName}. Skipping it.", table, databaseDetails.DatabaseName);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogInternalError(ex, "Failed to process database {DatabaseName} in cluster {ClusterUri}. Skipping it.", database, clusterDetails.ClusterUri);
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogInternalError(ex, "Failed to process cluster {ClusterUri}.", clusterDetails.ClusterUri);
                return false;
            }

            return true;
        }
    }


    [DurableTask]
    public class KustoDatabaseDiscoveryActivity : TaskActivity<KustoConnectionInfo, IEnumerable<string>>
    {
        private readonly ILogger<KustoDatabaseDiscoveryActivity> _logger;

        public KustoDatabaseDiscoveryActivity(ILogger<KustoDatabaseDiscoveryActivity> logger)
        {
            _logger = logger;
        }

        public override async Task<IEnumerable<string>> RunAsync(TaskActivityContext context, KustoConnectionInfo input)
        {
            _logger.LogInternalInformation("Getting all databases for cluster {ClusterUri}", input.ClusterUri);

            const string query = ".show databases";

            using IDataReader databasesData = await KustoTableIndexerDataConnector.KustoSummarizer!.PerformQueryAsync(string.Empty, query);

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
    }

    [DurableTask]
    public class KustoTableIndexTableDiscoveryActivity : TaskActivity<KustoDatabaseInput, IEnumerable<string>>
    {
        private readonly ILogger<KustoTableIndexColumnDescriptionActivity> _logger;

        public KustoTableIndexTableDiscoveryActivity(ILogger<KustoTableIndexColumnDescriptionActivity> logger)
        {
            _logger = logger;
        }

        public override async Task<IEnumerable<string>> RunAsync(TaskActivityContext context, KustoDatabaseInput input)
        {
            _logger.LogInternalInformation("Getting all tables for database {DatabaseName}", input.DatabaseName);

            const string query = ".show tables";

            using IDataReader tablesData = await KustoTableIndexerDataConnector.KustoSummarizer!.PerformQueryAsync(input.DatabaseName, query);

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
    }

    [DurableTask]
    public class KustoTableIndexTableSchemaActivity : TaskActivity<KustoTableIndexInput, KustoTableIndexColumnMetadata>
    {
        private readonly ILogger<KustoTableIndexTableSchemaActivity> _logger;

        public KustoTableIndexTableSchemaActivity(ILogger<KustoTableIndexTableSchemaActivity> logger)
        {
            _logger = logger;
        }

        public override async Task<KustoTableIndexColumnMetadata> RunAsync(TaskActivityContext context, KustoTableIndexInput input)
        {
            _logger.LogInternalInformation("Getting table schema for {DatabaseName}, {TableName}", input.DatabaseName, input.TableName);

            IEnumerable<KustoColumnMetadata> columnMetadata = await KustoTableIndexerDataConnector.KustoSummarizer!.GetTableSchemaAsync(input.DatabaseName, input.TableName);

            _logger.LogInternalInformation("Getting log message column names for {DatabaseName}, {TableName}", input.DatabaseName, input.TableName);

            IEnumerable<string> logMessageColumnNames = await KustoTableIndexerDataConnector.KustoSummarizer!.DiscoverLogMessageColumnsAsync(input.DatabaseName, input.TableName, columnMetadata.Select(x => x.Name));

            return new KustoTableIndexColumnMetadata(logMessageColumnNames, columnMetadata);
        }
    }

    [DurableTask]
    public class KustoTableIndexColumnDescriptionActivity : TaskActivity<KustoTableIndexColumnDescriptionInput, string>
    {
        private readonly ILogger<KustoTableIndexColumnDescriptionActivity> _logger;

        public KustoTableIndexColumnDescriptionActivity(ILogger<KustoTableIndexColumnDescriptionActivity> logger)
        {
            _logger = logger;
        }

        public override async Task<string> RunAsync(TaskActivityContext context, KustoTableIndexColumnDescriptionInput input)
        {
            _logger.LogInternalInformation("Getting description for column {ColumnName} for {DatabaseName}, {TableName}", input.ColumnName, input.DatabaseName, input.TableName);

            return await KustoTableIndexerDataConnector.KustoSummarizer!.CreateColumnDescriptionAsync(input.DatabaseName, input.TableName, input.ColumnName, input.ContextColumnNames);
        }
    }

    [DurableTask]
    public class KustoTableIndexSummarizeAndUploadActivity : TaskActivity<KustoTableIndexSummaryInput, bool>
    {
        private readonly ILogger<KustoTableIndexSummarizeAndUploadActivity> _logger;
        private readonly IAzureBlobStorageClient _blogStorageClient;

        public KustoTableIndexSummarizeAndUploadActivity(
            IAuthenticationService authService,
            IOptions<StorageSettings> storageSettings,
            ILogger<KustoTableIndexSummarizeAndUploadActivity> logger)
        {
            // Create a new client for this specific index using Managed Identity
            TokenCredential credential = authService.GetStorageCredential();
            _blogStorageClient = new AzureBlobStorageClient(new Uri(storageSettings.Value.BlobEndpoint), credential);

            _logger = logger;
        }

        public override async Task<bool> RunAsync(TaskActivityContext context, KustoTableIndexSummaryInput input)
        {
            _logger.LogInternalInformation("Getting table summary for {DatabaseName}, {TableName}", input.DatabaseName, input.TableName);

            List<KustoLogMessageSamples> logMessageSamples = new List<KustoLogMessageSamples>(input.Columns.LogMessageColumnNames.Count());
            foreach (string logMessageColumn in input.Columns.LogMessageColumnNames)
            {
                string logData = await KustoTableIndexerDataConnector.KustoSummarizer!.GetLogMessageSamplesAsync(input.DatabaseName, input.TableName, logMessageColumn);

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

            string tableSummary = await KustoTableIndexerDataConnector.KustoSummarizer!.SummarizeTableAsync(input.TableName, logMessageSamples, input.Columns.ColumnMetadata);

            _logger.LogInternalInformation("Getting example queries and description for {DatabaseName}", input.DatabaseName);

            IEnumerable<KustoExampleQueryAndDescription> exampleQueries = await GetExampleQueriesForTableAsync(input.DatabaseName, input.TableName, tableSummary);

            // Log each example query and its description
            foreach (var example in exampleQueries)
            {
                _logger.LogInternalDebug("Example Query for table {TableName}:\nDescription: {Description}\nQuery:\n{QueryText}",
                    input.TableName,
                    example.Description ?? "(No description)",
                    example.Query);
            }

            _logger.LogInternalInformation("Uploading table summary and example queries and description for {DatabaseName}, {TableName}", input.DatabaseName, input.TableName);

            await UploadJsonDocumentationAsync(input.ClusterUri, input.DatabaseName, input.TableName, tableSummary, logMessageSamples, input.Columns.ColumnMetadata, exampleQueries);

            return true;
        }

        private async Task UploadJsonDocumentationAsync(
            Uri clusterUri,
            string databaseName,
            string tableName,
            string tableDescription,
            IEnumerable<KustoLogMessageSamples> logMessageSamples,
            IEnumerable<KustoColumnMetadata> columns,
            IEnumerable<KustoExampleQueryAndDescription> exampleQueries)
        {
            _logger.LogInternalInformation("Generating JSON documentation for {DatabaseName}_{TableName}", databaseName, tableName);

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
                Id = $"{databaseName}_{tableName}", //id cannot have a ".", causes indexer to fail index the data from blob
                ClusterUri = clusterUri.ToString(),
                DatabaseName = databaseName,
                TableName = tableName,
                TableDescription = tableDescription,
                LogMessageSamples = new List<KustoLogMessageSamples>(logMessageSamples),
                Columns = new List<KustoColumnMetadata>(columns),
                MetadataConcat = sb.ToString()
            };

            await UploadJSONToBlob("kustometadata", $"{databaseName}/{tableName}.json", tableMetadata);

            var exampleQueryDoc = new KustoExampleQueryDocument
            {
                Id = $"{databaseName}_{tableName}_examplequeries",
                DatabaseName = databaseName,
                TableName = tableName,
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
                MetadataConcat = $"{tableDescription} {string.Join(" ", exampleQueries.Select(q => $"{q.Description} {q.Query}"))}"
            };

            await UploadJSONToBlob("kustometadataexamplequeries", $"{databaseName}/{tableName}_example_queries.json", exampleQueryDoc);

        }

        private async Task UploadJSONToBlob(string container, string blobName, object data)
        {
            await _blogStorageClient.UploadBlobContentsAsync(container, blobName, BinaryData.FromObjectAsJson(data));
            _logger.LogInternalInformation("Uploaded JSON documentation for {BlobName} to container {Container}", blobName, container);
        }

        public async Task<IEnumerable<KustoExampleQueryAndDescription>> GetExampleQueriesForTableAsync(string databaseName, string tableName, string tableDescription)
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
                var lines = File.ReadAllLines(file);
                var content = string.Join(Environment.NewLine, lines);

                if (!content.Contains(tableName, StringComparison.OrdinalIgnoreCase))
                    continue;

                string description = string.Empty;
                string queryText = content;

                if (lines.Length > 0 && lines[0].TrimStart().StartsWith("// Description:", StringComparison.OrdinalIgnoreCase))
                {
                    description = lines[0].Substring("// Description:".Length).Trim();
                    queryText = string.Join(Environment.NewLine, lines.Skip(1));
                }
                else
                {
                    // Generate description using AI if not present
                    description = await KustoTableIndexerDataConnector.KustoSummarizer!.GenerateQueryDescriptionAsync(tableDescription, queryText);
                }
                // Use a deterministic Id, e.g., databaseName_tableName_{i} or a hash of the query
                string id = $"{databaseName}_{tableName}_{Path.GetFileNameWithoutExtension(file)}";
                result.Add(new KustoExampleQueryAndDescription { Id = id, Description = description, Query = queryText });
            }

            return result;
        }
    }
}
