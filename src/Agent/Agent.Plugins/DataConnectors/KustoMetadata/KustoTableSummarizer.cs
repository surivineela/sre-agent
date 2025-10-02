// -----------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// -----------------------------------------------------------

using System.ClientModel;
using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Framework.Reasoning.Models;
using Agent.Plugins.Kusto;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Tools;
using Kusto.Data.Exceptions;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ChatMessage = Microsoft.Extensions.AI.ChatMessage;

namespace Agent.Plugins.DataConnectors.KustoMetadata
{
    public class KustoTableSummarizer
    {
        private const string SystemPrompt =
            """
            You are a log data specialist that understands log data stored in Kusto tables.
            You will answer any questions about the data from Kusto tables that are provided to you in an authoritative and confident manner.
            Do NOT give broad or general statements about the data. For example, do NOT say general things like the data is useful for troubleshooting, monitoring, or any other generic terms.
            Be specific about the data, its use cases, and what kind of questions it can answer.
            """;

        private readonly IChatClient _chatClient;
        private readonly KustoClient _kustoClient;
        private readonly ChatOptions _chatOptions;
        private readonly Uri _clusterUri;
        private readonly ILogger<KustoTableSummarizer> _logger;

        public KustoTableSummarizer(IChatClient chatClient, Uri clusterUri, string managedIdentityResourceId, DataConnectorSource dataConnectorSource, ILoggerFactory loggerFactory, IAuthenticationService authService)
        {
            _chatClient = chatClient;
            _clusterUri = clusterUri;
            _logger = loggerFactory.CreateLogger<KustoTableSummarizer>();
            _kustoClient = BuildKustoClient(loggerFactory, authService, managedIdentityResourceId, dataConnectorSource);

            _chatOptions = new ChatOptions()
            {
                Temperature = 0.7f
            };
        }

        public async Task<IDataReader> PerformQueryAsync(string database, string query, CancellationToken cancellationToken)
        {
            const int MaxAttempts = 3;
            KustoClientException? lastException = null;

            for (int i = 0; i < MaxAttempts; ++i)
            {
                try
                {
                    return await _kustoClient.PerformQueryAsync(_clusterUri.ToString(), database, query, cancellationToken);
                }
                catch (KustoClientException ex)
                {
                    _logger.LogInternalWarning(ex, $"An error occurred while executing PerformQueryAsync: {ex.Message}");

                    lastException = ex;
                }
            }

            if (lastException != null)
            {
                throw lastException;
            }

            throw new InvalidOperationException("Failed to execute query after multiple attempts.");
        }

        public async Task<string> GetLogMessageSamplesAsync(string databaseName, string tableName, string logMessageColumnName, string timestampColumnName, CancellationToken cancellationToken)
        {
            const string columnSummaryPrompt =
                $$"""
                # Instructions
                Using the log table rows below, return a new-line separated list of semantically unique rows with no duplicates and nothing else in your response.

                ## Examples
                Duplicate rows can by identified by having a similar pattern with differing values filled in for things like names, IDs, dates, etc. For example, the following would be considered duplicates:

                Example 1:
                "volume: static-files-volume is emptyDir for container app or job f3d3580e-bcee-464f-8e3e-b10aa98b2bc"
                "volume: pgdata is emptyDir for container app or job 613b1415-ab36-4f08-9558-48f5b9d46edd"

                Example 2:
                "Cluster yellowsky-d51538f5 was created on 2025-02-03 at 11:24:42Z"
                "Cluster thankfulwave-d0b9e1d6 was created on 2025-02-04 at 03:10:02Z"

                Example 3:
                I0627 23:20:07.831154 1 request.go:697] Waited for 2.582681249s due to client-side throttling, not priority and fairness, request: PUT:https://100.100.224.1:443/api/v1/namespaces/k8se-apps/events/worker11antsscale010-l6x9e71-gnw95.cad96cea6fd8d5444487337t48z2
                I0628 00:26:53.826876 1 request.go:697] Waited for 1.033386051s due to client-side throttling, not priority and fairness, request: PUT:https://100.100.128.1:443/api/v1/namespaces/k8se-apps/events/largereplicasapp--gwleybt-bc7745f66-4rfz4.4743d8502854de8fbtlnn

                Notice how in each of these examples, the rows looks like they're saying the same thing but with different values such as IDs, names, dates, or other values.
                The duplicates should be removed, and only unique rows should be returned.

                # Log table rows

                """;

            if (string.IsNullOrEmpty(logMessageColumnName))
            {
                return string.Empty;
            }

            for (int i = 0; i < 3; ++i)
            {
                // start with a substring length of 15 characters and reduce it if there are too many duplicates. A smaller substring will result in more aggressive de-duping at the cost of potentially losing unique records that happen to start or end with the same substring.
                int subStringLength = 15 - i * 3;

                try
                {

                    // This query does some de-duplication of log messages by taking the first x characters of the log message and summarizing by that substring, then summarizing again by the last x characters.
                    // The LLM will further de-duplicate the messages based on the context it has
                    using IDataReader dataReader = await PerformQueryAsync(
                        databaseName,
                        $$"""
                        {{tableName}}
                            | where {{timestampColumnName}} > ago (7d)
                            | where isnotempty({{logMessageColumnName}})
                            | project {{logMessageColumnName}}
                            | extend sub1 = substring({{logMessageColumnName}}, 0, {{subStringLength}})
                            | summarize take_any({{logMessageColumnName}}) by sub1
                            | project {{logMessageColumnName}}, sub2 = substring({{logMessageColumnName}}, strlen({{logMessageColumnName}})-{{subStringLength}})
                            | summarize take_any({{logMessageColumnName}}) by sub2
                            | project {{logMessageColumnName}}
                            | sample 1000
                            | sort by {{logMessageColumnName}} asc
                        """,
                        cancellationToken);

                    StringBuilder sb = new StringBuilder();
                    List<string> batchResults = new List<string>();

                    while (dataReader.Read())
                    {
                        cancellationToken.ThrowIfCancellationRequested();

                        string? line = dataReader[0].ToString();
                        if (!string.IsNullOrEmpty(line))
                        {
                            int newLineIndex = line.IndexOf('\n');
                            if (newLineIndex >= 0)
                            {
                                line = line[..newLineIndex];
                            }

                            line = line[..Math.Min(line.Length, 1000)]; // Limit to 1000 characters to avoid too long lines

                            sb.AppendLine(line);

                            // Check if we need to send a batch
                            if (sb.Length > 30000)
                            {
                                // Send current batch
                                string batchPrompt = columnSummaryPrompt + Environment.NewLine + sb.ToString();
                                string batchResult = await SendToModel(batchPrompt, cancellationToken);
                                batchResults.Add(batchResult);

                                // Clear the buffer for next batch
                                sb.Clear();
                            }
                        }
                    }

                    // Process any remaining data
                    if (sb.Length > 0)
                    {
                        string finalPrompt = columnSummaryPrompt + Environment.NewLine + sb.ToString();
                        string finalResult = await SendToModel(finalPrompt, cancellationToken);
                        batchResults.Add(finalResult);
                    }

                    if (batchResults.Count > 0)
                    {
                        return string.Join(Environment.NewLine, batchResults);
                    }
                }
                catch (KustoServicePartialQueryFailureException ex)
                {
                    // this typically means the de-duped set was still too large to summarize.
                    _logger.LogInternalWarning(ex, $"Partial query failure when getting example log mesages with substring size {subStringLength}. {ex.Message}");
                }
            }

            return string.Empty;
        }

        public async Task<string> SummarizeTableAsync(string tableName, IEnumerable<KustoLogMessageSamples> logMessageSamples, IEnumerable<KustoColumnMetadata> columnMetadata, CancellationToken cancellationToken)
        {
            StringBuilder logData = new StringBuilder();

            if (logMessageSamples.Any())
            {
                foreach (KustoLogMessageSamples sample in logMessageSamples)
                {
                    if (sample.UniqueMessages.Count > 0)
                    {
                        logData.AppendLine($"## {sample.LogColumnName}");
                        logData.AppendLine();
                        logData.AppendLine(string.Join(Environment.NewLine, sample.UniqueMessages));
                        logData.AppendLine();
                    }
                }
            }

            StringBuilder finalPrompt = new StringBuilder(
                $$"""
                Provide a summary of the following log table named {{tableName}}.
                Include details about the kind of data found in the table and what kind of questions the data can answer.
                """);

            if (logData.Length > 0)
            {
                finalPrompt.Append(
                    """
                    Use the sample of unique log messages as the pimary context for your summary, followed by the table schema as additional context.
                    Be concise and specific in your answer. Start with a concise summary paragraph and then follow that with a longer summary about the log messages.
                    """);
            }
            else
            {
                finalPrompt.Append(
                    """
                    Use the table schema as the pimary context for your summary.
                    Be concise and specific in your answer. Limit your summary to two paragraphs or less.
                    """);
            }

            finalPrompt.AppendLine(
                $$"""
                Include details about the kind of data found in the table and what kind of questions the data can answer.
                Be concise and specific in your answer.
                """);

            finalPrompt.AppendLine("# Sample of unique log messages");
            finalPrompt.AppendLine();
            finalPrompt.AppendLine("The following data shows a comprehensive sample of unique log messages in the table. Use this as the primary context for your summary.");
            finalPrompt.AppendLine();
            finalPrompt.AppendLine(logData.ToString());

            finalPrompt.AppendLine();
            finalPrompt.AppendLine("# Table schema");
            finalPrompt.AppendLine();
            finalPrompt.AppendLine("The following schema shows all of the columns in the table, their data types, and a brief summary of the data they contain.");

            foreach (KustoColumnMetadata column in columnMetadata)
            {
                finalPrompt.AppendLine(CultureInfo.InvariantCulture, $"Column: {column.Name}, Type: {column.Type}, Description: {column.Description}");
            }

            return await SendToModel(finalPrompt.ToString(), cancellationToken);
        }

        public async Task<string> GenerateQueryDescriptionAsync(string tableDescription, string queryText, CancellationToken cancellationToken)
        {
            // in this prompt , we can also add column metadata, a refinement would be to parse the query for the columns it projects and then use only that speicifc metadata
            string prompt = $@"
                    Given the following Kusto table description and query, provide a concise description of what the query does and what kind of insight it provides.

                        Table Description:
                        {tableDescription}

                        Kusto Query:
                        {queryText}

                        Description:";

            return await SendToModel(prompt, cancellationToken);
        }

        public async Task<IReadOnlyList<KeyValuePair<string, string>>> GetTableSchemaAsync(string databaseName, string tableName, CancellationToken cancellation)
        {
            string query = $".show table {tableName} schema as json ";
            using IDataReader result = await PerformQueryAsync(databaseName, query, cancellation);

            if (result.Read())
            {
                int schemaColumnIndex = result.GetOrdinal("Schema");
                string schemaJson = result.GetString(schemaColumnIndex);
                JsonElement json = JsonDocument.Parse(schemaJson).RootElement;

                if (json.TryGetProperty("OrderedColumns", out JsonElement columnsElement) && columnsElement.ValueKind == JsonValueKind.Array)
                {
                    List<KeyValuePair<string, string>> columns = new List<KeyValuePair<string, string>>(columnsElement.GetArrayLength());

                    foreach (JsonElement column in columnsElement.EnumerateArray())
                    {
                        string name = column.GetProperty("Name").GetString() ?? string.Empty;
                        string type = column.GetProperty("CslType").GetString() ?? string.Empty;

                        columns.Add(new KeyValuePair<string, string>(name, type));
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

        public async Task<string> CreateColumnDescriptionAsync(string databaseName, string tableName, string columnName, string timestampColumnName, IEnumerable<string> contextColumns, CancellationToken cancellationToken)
        {
            IEnumerable<string> contextColumnsWrapped = contextColumns
                    .Where(x => !string.Equals(x, columnName))
                    .Select(WrapColumnName);

            string contextColumnString = string.Join(",", contextColumnsWrapped);
            string selectColumns = string.Join(",", contextColumnsWrapped.Append(WrapColumnName(columnName)));

            _logger.LogInternalInformation($"Getting column details for table: {tableName}, column: {columnName} using context columns: {contextColumnString}");

            using IDataReader dataReader = await PerformQueryAsync(
                databaseName,
                $"{tableName} | where isnotempty({WrapColumnName(columnName)}) and {timestampColumnName} > ago(14d) | project {selectColumns} | sample 1000 | distinct {selectColumns} | take 100",
                cancellationToken);

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
            return await SendToModel(query.ToString(), cancellationToken);
        }

        public async Task<string> DiscoverTimeStampColumnsAsync(string databaseName, string tableName, IReadOnlyList<KeyValuePair<string, string>> columnSchema, CancellationToken cancellationToken)
        {
            _logger.LogInternalInformation("Getting timestamp columns for table: {TableName}", tableName);

            List<string> badColumns = new List<string>();
            for (int i = 0; i < 3; ++i)
            {
                StringBuilder sb = new StringBuilder(
                    """
                    # Instructions
                    Given the following list of Kusto table column schema and sample data, identify the column that is most likely to represent a timestamp indicating the exact moment the log entry was generated.

                    If there are multiple columns that could represent a timestamp, use the following preferences in order of priority:
                    1. Prefer a column that has a type of 'datetime' over any other type.
                    2. If there are multiple columns of type 'datetime', prefer a column that has a high precision.

                    Return ONLY the name of ONE column wrapped in ['']. If the only column that could represent a timestamp is not of type 'datetime', return the name of that column anyway and wrap it in todatetime([''])

                    If no column can be identified as a timestamp, return an empty string.

                    """);

                if (badColumns.Count > 0)
                {
                    sb.AppendLine($"Note: Ignore the following columns as they are not good timestamp columns: {string.Join(',', badColumns)}");
                }

                sb.AppendLine();
                sb.AppendLine("# Column schema");
                sb.AppendLine();

                foreach (KeyValuePair<string, string> column in columnSchema)
                {
                    sb.AppendLine(CultureInfo.InvariantCulture, $"Column: {column.Key}, Type: {column.Value}");
                }

                using (IDataReader dataReader = await _kustoClient.PerformQueryAsync(
                    _clusterUri.ToString(),
                    databaseName,
                    $"{tableName} | take 1",
                    cancellationToken))
                {
                    KustoQueryResult kustoSampleData = new KustoQueryResult(dataReader, string.Empty);

                    sb.AppendLine();
                    sb.AppendLine("# Sample data from the table");
                    sb.AppendLine();
                    sb.AppendLine(kustoSampleData.Result);
                }

                string timeStampColumn = await SendToModel(sb.ToString(), cancellationToken);

                _logger.LogInternalInformation("Iteration {Iteration}: Got timestamp column: {Column}. Trying it out..", i, timeStampColumn);

                // test the timeStampColumn to make sure it's valid
                try
                {
                    using IDataReader dataReader = await _kustoClient.PerformQueryAsync(
                        _clusterUri.ToString(),
                        databaseName,
                        $"{tableName} | where {timeStampColumn} > ago(7d) | take 1",
                        cancellationToken);

                    int rowCount = 0;
                    while (dataReader.Read())
                    {
                        ++rowCount;
                    }

                    if (rowCount == 1)
                    {
                        _logger.LogInternalInformation("Iteration {Iteration}: Timestamp column: {Column} looks good.", i, timeStampColumn);

                        return timeStampColumn;
                    }

                    _logger.LogInternalInformation("Iteration {Iteration}: Timestamp column: {Column} - no rows returned. Trying again.", i, timeStampColumn);

                    badColumns.Add(timeStampColumn);
                }
                catch (KustoRequestException ex)
                {
                    _logger.LogInternalInformation("Iteration {Iteration}: Query with timestamp column: {Column} was invalid. Reason: {Message}", i, timeStampColumn, ex.Message);

                    badColumns.Add(timeStampColumn);
                }
                catch (Exception ex) when (ex.IsNotTokenCancellation(cancellationToken))
                {
                    // some other error, try again
                    _logger.LogInternalWarning(ex, "Iteration {Iteration}: Failed to run query with timestamp column: {Column}. Reason: {Message}", i, timeStampColumn, ex.Message);
                }
            }

            return string.Empty;
        }

        public async Task<IEnumerable<string>> DiscoverLogMessageColumnsAsync(string databaseName, string tableName, IEnumerable<string> columnNames, string timeStampColumnName, CancellationToken cancellationToken)
        {
            _logger.LogInternalInformation("Getting message columns for table: {TableName}", tableName);

            using IDataReader dataReader = await _kustoClient.PerformQueryAsync(
                _clusterUri.ToString(),
                databaseName,
                $" {tableName} | where {timeStampColumnName} > ago(7d) | sample 100",
                cancellationToken);

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

                string result = await SendToModel(query.ToString(), cancellationToken);

                _logger.LogInternalDebug($"Table '{tableName}' column results: {result}");

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

        private async Task<string> SendToModel(string prompt, CancellationToken cancellationToken)
        {
            int maxRetryAttempts = 5;
            int throttleDelaySeconds = 5;
            for (int i = 1; ; ++i)
            {
                try
                {
                    ChatMessage userMessage = new ChatMessage(ChatRole.User, prompt);

                    List<ChatMessage> messages = new List<ChatMessage>
                    {
                        new ChatMessage(ChatRole.System, SystemPrompt),
                        userMessage
                    };

                    ChatResponse response = await _chatClient.GetResponseAsync(messages, _chatOptions, cancellationToken);

                    return response.Messages.Last().Text;
                }
                catch (ClientResultException ex)
                {
                    if (i >= maxRetryAttempts)
                    {
                        _logger.LogInternalError(ex, "Failed to get response from model after {MaxRetryAttempts} attempts. Last error: {Message}", maxRetryAttempts, ex.Message);
                        throw;
                    }

                    if (ex.Status == 429)
                    {
                        // getting throttled by Open AI
                        await Task.Delay(TimeSpan.FromSeconds(throttleDelaySeconds * i), cancellationToken);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }

        private static KustoClient BuildKustoClient(ILoggerFactory loggerFactory, IAuthenticationService authService, string managedIdentityResourceId, DataConnectorSource dataConnectorSource)
        {
            ConnectorAuthSettings authSettings;

            if (dataConnectorSource == DataConnectorSource.AgentSpace)
            {
                authSettings = new ConnectorAuthSettings()
                {
                    AuthenticationType = ConnectorAuthType.AgentSpace,
                    ManagedIdentityResourceId = managedIdentityResourceId
                };
            }
            else if (string.IsNullOrEmpty(managedIdentityResourceId))
            {
                authSettings = new ConnectorAuthSettings()
                {
                    AuthenticationType = ConnectorAuthType.User
                };
            }
            else
            {
                authSettings = new ConnectorAuthSettings()
                {
                    AuthenticationType = ConnectorAuthType.UAMI,
                    ManagedIdentityResourceId = managedIdentityResourceId
                };
            }

            return new KustoClient(loggerFactory.CreateLogger<KustoClient>(), new KustoConnector()
            {
                Auth = authSettings
            }, authService);
        }
    }
}
