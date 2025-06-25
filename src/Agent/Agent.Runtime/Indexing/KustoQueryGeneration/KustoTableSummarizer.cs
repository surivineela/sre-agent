// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Agent.Core.Models.Search;
using Agent.Plugins.Kusto;
using Agent.Plugins.KustoPlugin;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Runtime.Indexing.KustoQueryGeneration
{
    public class KustoTableSummarizer
    {
        private const string SystemPrompt =
            """
            You are a Kusto (Azure Data Explorer) master that understands table data from Kusto.
            You will answer any questions about the data from Kusto tables that are provided to you in an authoritative and confident manner.
            Do NOT give broad or general statements about the data. For example, do NOT say general things like the data is useful for troubleshooting, monitoring, or any other generic terms.
            Be specific about the data, its use cases, and what kind of questions it can answer.
            """;

        private readonly IChatClient _chatClient;
        private readonly KustoClient _kustoClient;
        private readonly ChatOptions _chatOptions;
        private readonly Uri _clusterUri;
        private readonly ILogger<KustoTableSummarizer> _logger;

        public KustoTableSummarizer(IChatClient chatClient, Uri clusterUri, string managedIdentityResourceId, ILoggerFactory loggerFactory)
        {
            _chatClient = chatClient;
            _clusterUri = clusterUri;
            _logger = loggerFactory.CreateLogger<KustoTableSummarizer>();
            _kustoClient = BuildKustoClient(loggerFactory, managedIdentityResourceId);

            _chatOptions = new ChatOptions()
            {
                Temperature = 0.7f
            };
        }

        public async Task<IDataReader> PerformQueryAsync(string database, string query)
        {
            return await _kustoClient.PerformQueryAsync(_clusterUri.ToString(), database, query);
        }

        public async Task<string> SummarizeTableAsync(string tableName, IEnumerable<KustoColumnMetadata> columnMetadata)
        {
            StringBuilder query = new StringBuilder(
                """
                Provide a summary of the following Kusto table based on the table name and information about the data in each column.
                Include details about the kind of data found in the table, use cases for the data, and what kind of questions the data can answer.
                Be concise. Limit the summary to no more than a paragraph or two.
                """);

            query.AppendLine();
            query.AppendLine(CultureInfo.InvariantCulture, $"Table `{tableName}");
            foreach (KustoColumnMetadata column in columnMetadata)
            {
                query.AppendLine(CultureInfo.InvariantCulture, $"Column: {column.Name}, Type: {column.Type}, Description: {column.Description}");
            }

            return await SendToModel(query.ToString());
        }

        public async Task<string> GenerateQueryDescriptionAsync(string tableDescription, string queryText)
        {

            // in this prompt , we can also add column metadata, a refinement would be to parse the query for the columns it projects and then use only that speicifc metadata
            var prompt = $@"
                    Given the following Kusto table description and query, provide a concise description of what the query does and what kind of insight it provides.

                        Table Description:
                        {tableDescription}

                        Kusto Query:
                        {queryText}

                        Description:";
            // Use your AI chat client to get the description
            return await SendToModel(prompt);
        }

        public async Task<IEnumerable<KustoColumnMetadata>> GetTableSchemaAsync(string databaseName, string tableName)
        {
            string query = $".show table {tableName} schema as json ";
            using IDataReader result = await PerformQueryAsync(databaseName, query);

            if (result.Read())
            {
                int schemaColumnIndex = result.GetOrdinal("Schema");
                string schemaJson = result.GetString(schemaColumnIndex);
                JsonElement json = JsonDocument.Parse(schemaJson).RootElement;

                if (json.TryGetProperty("OrderedColumns", out JsonElement columnsElement) && columnsElement.ValueKind == JsonValueKind.Array)
                {
                    List<KustoColumnMetadata> columns = new List<KustoColumnMetadata>(columnsElement.GetArrayLength());

                    foreach (JsonElement column in columnsElement.EnumerateArray())
                    {
                        string name = column.GetProperty("Name").GetString() ?? string.Empty;
                        string type = column.GetProperty("CslType").GetString() ?? string.Empty;

                        columns.Add(new KustoColumnMetadata()
                        {
                            Name = name,
                            Type = type,
                            Description = string.Empty
                        });
                    }

                    return columns;
                }
            }

            throw new InvalidOperationException($"Failed to retrieve schema for table '{tableName}'");
        }

        private static string WrapColumnName(string columnName)
        {
            return $"['{columnName}']";
        }

        public async Task<string> CreateColumnDescriptionAsync(string databaseName, string tableName, string columnName, IEnumerable<string> contextColumns)
        {
            IEnumerable<string> contextColumnsWrapped = contextColumns
                    .Where(x => !string.Equals(x, columnName))
                    .Select(WrapColumnName);

            string contextColumnString = string.Join(",", contextColumnsWrapped);
            string selectColumns = string.Join(",", contextColumnsWrapped.Append(WrapColumnName(columnName)));

            _logger.LogInternalInformation($"Getting column details for table: {tableName}, column: {columnName} using context columns: {contextColumnString}");

            using IDataReader dataReader = await PerformQueryAsync(
                databaseName,
                $"{tableName} | where isnotempty({WrapColumnName(columnName)}) and TIMESTAMP > ago(14d) | project {selectColumns} | sample 1000 | distinct {selectColumns} | take 100");

            KustoQueryResult result = new KustoQueryResult(dataReader, string.Empty);

            if (string.IsNullOrEmpty(result.Result))
            {
                return string.Empty;
            }

            StringBuilder query = new StringBuilder(
                $$"""
                The following is sample data from a Kusto table. Figure out what the data in the `{{columnName}}` column means and what it can be used for. {{(contextColumns.Any() ? $" Use the data in the {contextColumnString} columns as context to better understand the data in {columnName}." : "")}}
                Include details about the kind of data found in the `{{columnName}}` column, usecases for the data, and what kind of questions the data can answer.
                Use authoritative and confident language. Do NOT say things like "appears to represent" or "might be".
                Summarize your description in 5 sentences or less. Do not include numbered or bulleted lists.
                """);

            query.AppendLine();
            query.AppendLine(result.Result);

            // TODO: log data can be too verbose causing LLM token error. Need way to fetch most relevant data (maybe can refer back to column metadata in search?)
            return await SendToModel(query.ToString());
        }

        public async Task<IEnumerable<string>> DiscoverLogMessageColumnsAsync(string databaseName, string tableName, IEnumerable<string> columnNames)
        {
            _logger.LogInternalInformation("Getting message columns for table: {TableName}", tableName);

            using IDataReader dataReader = await _kustoClient.PerformQueryAsync(
                _clusterUri.ToString(),
                databaseName,
                $" {tableName} | where TIMESTAMP > ago(7d) | sample 100");

            KustoQueryResult kustoSampleData = new KustoQueryResult(dataReader, string.Empty);

            string badColumns = string.Empty;
            string question =
                """
                    Based on the provided data, which columns in the following table data have a textual log message that describes in plain language what happened in each event?
                    There may be more than one, or there may be none. Respond with only the names of the column separated by commas and nothing else.
                    """;

            for (int i = 0; i < 3; ++i)
            {
                StringBuilder query = new StringBuilder(question);

                if (!string.IsNullOrEmpty(badColumns))
                {
                    query.AppendLine("Note: The following columns are not in the schema: " + badColumns);
                }
                
                query.AppendLine();
                query.AppendLine(kustoSampleData.Result);

                string result = await SendToModel(query.ToString());

                _logger.LogInternalInformation($"Table '{tableName}' column results: {result}");

                string[] columns = result.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

                // check if the columns are in the schema
                badColumns = string.Join(", ", columns.Where(c => !columnNames.Any(col => col.Equals(c, StringComparison.OrdinalIgnoreCase))));

                if (string.IsNullOrWhiteSpace(badColumns))
                {
                    // all columns are in the schema
                    // use the original column names to ensure the right casing
                    return columnNames.Where(column => columns.Contains(column, StringComparer.OrdinalIgnoreCase));
                }
                else
                {
                    // some columns are not in the schema, try again
                    _logger.LogInternalWarning("Some columns are not in the schema: {BadColumns}", badColumns);
                }
            }

            return Enumerable.Empty<string>();

        }

        private async Task<string> SendToModel(string prompt)
        {
            ChatMessage userMessage = new ChatMessage(ChatRole.User, prompt);

            List<ChatMessage> messages = new List<ChatMessage>
            {
                new ChatMessage(ChatRole.System, SystemPrompt),
                userMessage
            };

            ChatResponse response = await _chatClient.GetResponseAsync(messages, _chatOptions);

            return response.Messages.Last().Text;
        }

        private static KustoClient BuildKustoClient(ILoggerFactory loggerFactory, string managedIdentityResourceId)
        {
            KustoAuthSettings authSettings = string.IsNullOrEmpty(managedIdentityResourceId) ?
                new KustoAuthSettings()
                {
                    AuthenticationType = KustoAuthenticationType.User
                } :
                new KustoAuthSettings()
                {
                    AuthenticationType = KustoAuthenticationType.UAMI,
                    ManagedIdentityResourceId = managedIdentityResourceId
                };

            return new KustoClient(loggerFactory.CreateLogger<KustoClient>(), new KustoSettings()
            {
                Auth = authSettings
            });
        }
    }
}
