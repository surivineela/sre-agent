// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Data;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Agent.Core;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DataModels;
using Agent.Plugins.Interface;
using Agent.Plugins.KustoPlugin;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Kusto
{
    public class SamplingOptions
    {
        public string Technique { get; set; } = "none"; // e.g., "top", "percent", "interval"
        public int? TopN { get; set; }
        public int? SamplePercent { get; set; }
        public int? IntervalMinutes { get; set; }

        public bool HasValue =>
            (!string.IsNullOrEmpty(Technique) && Technique != "none") &&
            (TopN.HasValue || SamplePercent.HasValue || IntervalMinutes.HasValue);
    }

    public static class SamplingParameterHelper
    {
        public static void AddSamplingParameters(
            Dictionary<string, string> parameters,
            SamplingOptions? sampling)
        {
            if (sampling == null || !sampling.HasValue)
                return;

            if (!string.IsNullOrEmpty(sampling.Technique))
                parameters["samplingTechnique"] = sampling.Technique;

            if (sampling.TopN.HasValue)
                parameters["top"] = sampling.TopN.Value.ToString();

            if (sampling.SamplePercent.HasValue)
                parameters["samplePercent"] = sampling.SamplePercent.Value.ToString();

            if (sampling.IntervalMinutes.HasValue)
                parameters["intervalMinutes"] = sampling.IntervalMinutes.Value.ToString();
        }
    }

    [AgentToolPlugin(IsFirstPartyOnly = true, Category = ToolCategories.LogQuery)]
    public partial class KustoPlugin : IKustoPlugin
    {
        private readonly ILogger<KustoPlugin> _logger;
        private readonly KustoClient _kustoClient;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;
        private const int TokenLimit = 200000;

        [GeneratedRegex("[^a-zA-Z\\d]")]
        private static partial Regex RegionNormalizationRegex();

        public KustoCluster GetCluster(string region, string groupName)
        {
            if (string.IsNullOrWhiteSpace(region))
                throw new ArgumentException("Region must be provided.", nameof(region));

            KustoRegionalGroupSettings? group = _kustoClient.KustoSettings.RegionalClusterGroups?.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(groupName))
                group = _kustoClient.KustoSettings.RegionalClusterGroups?.FirstOrDefault(c => string.Equals(c.Name, groupName, StringComparison.OrdinalIgnoreCase));
            if (group == null)
                throw new InvalidOperationException($"Kusto group '{groupName}' not found in the settings.");

            var cluster = group.Regions
                .FirstOrDefault(r => string.Equals(r.Region, region, StringComparison.OrdinalIgnoreCase));

            if (cluster == null)
                throw new InvalidOperationException($"Region '{region}' is not configured in Kusto settings for group '{groupName}'.");

            return cluster;
        }

        public KustoPlugin(ILogger<KustoPlugin> logger, KustoClient kustoClient, IAgentOutboundCommunicationService agentOutboundCommunicationService)
        {
            _logger = logger;
            _kustoClient = kustoClient;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
        }


        [KernelFunction("execute_kusto_query_on_cluster")]
        [Description("Executes a fully qualified Kusto query on a specific cluster and database, returning the result in JSON format.")]
        public async Task<string> ExecuteClusterKustoQuery(
            [Description("The short name of the target Kusto cluster (without URL schema or suffix).")] string cluster,
            [Description("The name of the target Kusto database.")] string database,
            [Description("The full Kusto query to execute.")] string fullQuery
            )
        {
            return (await ExecuteClusterKustoQueryInternal(cluster, database, fullQuery)).Result;

        }

        [KernelFunction("execute_kusto_query")]
        [Description("Executes a Kusto query on a regional cluster and returns the result as a JSON string.")]
        public async Task<string> ExecuteKustoQuery(
            [Description("The region of the target Kusto cluster.")] string region,
            [Description("The Kusto query to execute.")] string query,
            [Description("Optional group name for the Kusto cluster.")] string? groupName
            )
        {
            return (await ExecuteKustoQueryInternal(region, query, groupName)).Result;
        }


        public async Task<KustoQueryResult> ExecuteKustoQueryInternal(
            string region,
           string query,
            string? groupName
            )
        {
            try
            {
                if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(query))
                {
                    throw new ArgumentException("Region and query must be provided.");
                }
                if (_kustoClient.KustoSettings.RegionalClusterGroups.Count == 0)
                {
                    _logger.LogInternalError("No regional clusters are configured in Kusto settings.");
                    throw new InvalidOperationException("No regional clusters are configured in Kusto settings.");
                }
                var cluster = GetCluster(region, groupName ?? string.Empty);

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                _logger.LogInternalInformation($"execute_kusto_query called with {region} / {query}");

                var normalizedRegion = RegionNormalizationRegex().Replace(region, string.Empty).ToLowerInvariant();
                using var reader = await _kustoClient.PerformQueryAsync(cluster.ClusterUri ?? string.Empty, cluster.Database ?? string.Empty, query);

                stopwatch.Stop();
                var ret = new KustoQueryResult(reader, query);
                ret.Message = CreateChatMessage(query, normalizedRegion, ret.RowCount, (int)stopwatch.ElapsedMilliseconds, groupName: groupName ?? string.Empty);

                return ret;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError($"An error occurred while executing Kusto Query: {ex.Message}");
                return CreateErrorResult(query, ex.Message);
            }
        }


        internal static string GetKqlFilePath(string functionName, string baseDirectory)
        {
            var baseDir = Path.Combine(baseDirectory, "Plugins", "Definitions", "Queries");

            // Maintain existing behavior: direct file name search (backward compatibility)
            var directPath = Path.Combine(baseDir, $"{functionName}.kql");
            if (File.Exists(directPath))
            {
                return directPath;
            }

            // New feature: namespace format only
            if (functionName.Contains('.'))
            {
                var parts = functionName.Split('.');
                var kqlFileName = parts.Last() + ".kql";
                var subDirs = parts.Take(parts.Length - 1).ToArray();

                var namespacedFile = Path.Combine(new[] { baseDir }.Concat(subDirs).Concat(new[] { kqlFileName }).ToArray());
                if (File.Exists(namespacedFile))
                {
                    return namespacedFile;
                }
            }

            // Return the same path as existing behavior (File.Exists check is done by caller)
            return directPath;
        }

        Task<List<KustoFunctionInfo>> IKustoPlugin.ListFunctionsAsync(string region)
        {
            region = region.NormalizeLocation();
            return ListFunctionsAsync(region);
        }

        [KernelFunction("list_kusto_functions")]
        [Description("Lists all available user-defined functions from the Kusto cluster for a given region, including metadata like name, folder, and description.")]
        public async Task<List<KustoFunctionInfo>> ListFunctionsAsync(
            [Description("The region of the Kusto cluster to query.")] string region)
        {
            var query = ".show functions | project Name, Folder, DocString, Parameters";
            var result = new List<KustoFunctionInfo>();
            var cluster = GetCluster(region, string.Empty);
            using var reader = await _kustoClient.PerformQueryAsync(cluster.ClusterUri ?? string.Empty, cluster.Database ?? string.Empty, query);
            while (reader.Read())
            {
                result.Add(new KustoFunctionInfo
                {
                    Name = reader["Name"]?.ToString() ?? string.Empty,
                    Folder = reader["Folder"]?.ToString() ?? string.Empty,
                    DocString = reader["DocString"]?.ToString() ?? string.Empty,
                    Parameters = reader["Parameters"]?.ToString() ?? string.Empty
                });
            }

            return result;
        }

        [KernelFunction("execute_kusto_function")]
        [Description("Executes a user-defined Kusto function with named arguments on the regional Kusto cluster and returns the results.")]
        public async Task<string> ExecuteFunctionAsync(
            [Description("The name of the Kusto function to invoke.")] string functionName,
            [Description("The region of the Kusto cluster.")] string region,
            Dictionary<string, string>? args = null,
            [Description("Optional group name for the Kusto cluster.")] string? groupName = null)
        {
            return (await ExecuteFunctionInternalAsync(functionName, region, args, groupName)).Result;
        }


        public async Task<KustoQueryResult> ExecuteFunctionInternalAsync(
         string functionName,
      string region,
        Dictionary<string, string>? args = null,
        string? groupName = null)
        {
            string argList = args != null && args.Count > 0
                ? string.Join(", ", args.Select(kvp => $"{kvp.Key}={QuoteIfNeeded(kvp.Value)}"))
                : "";

            var query = string.IsNullOrEmpty(argList) ? $"{functionName}()" : $"{functionName}({argList})";

            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var cluster = GetCluster(region, groupName ?? string.Empty);
                using var reader = await _kustoClient.PerformQueryAsync(cluster.ClusterUri ?? string.Empty, cluster.Database ?? string.Empty, query);
                var ret = new KustoQueryResult(reader, query);
                stopwatch.Stop();
                ret.Message = CreateChatMessage(query, region, ret.RowCount, (int)stopwatch.ElapsedMilliseconds, functionName: functionName, groupName: groupName ?? string.Empty);
                return ret;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError($"An error occurred while executing Kusto Function {functionName}: {ex.Message}");
                return CreateErrorResult(query, ex.Message, functionName);
            }
        }
        private static string QuoteIfNeeded(string value)
        {
            return $"\"{value}\"";
        }

        public ChatMessage CreateChatMessage(string query, string regionOrClusterUri, int count, int queryExecutionTimeInMilliSeconds, string? database = null, string? functionName = null, string? groupName = null)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                throw new ArgumentNullException(nameof(query));
            }

            if (string.IsNullOrWhiteSpace(regionOrClusterUri))
            {
                throw new ArgumentNullException(regionOrClusterUri, nameof(regionOrClusterUri));
            }

            string adxUri = regionOrClusterUri;
            if (regionOrClusterUri.IndexOf(".kusto.", StringComparison.OrdinalIgnoreCase) < 0)
            {
                // Supplied parameter is a region, lookup cluster URI for the region
                var region = regionOrClusterUri;

                // TODO: update this plugin with a parameter to allow querying regional clusters for other products

                KustoCluster? cluster = null;
                try
                {
                    cluster = GetCluster(region, groupName ?? string.Empty);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError($"An error occurred while getting Kusto cluster for region {region}: {ex.Message}");
                }

                if (cluster != null)
                {
                    adxUri = cluster.ClusterUri ?? string.Empty;
                    database = cluster.Database;
                }
            }
            else
            {
                // Supplied parameter is a cluster URI, extract the cluster name
                adxUri = regionOrClusterUri;
                if (string.IsNullOrWhiteSpace(database))
                {
                    throw new ArgumentNullException(nameof(database), "Database name is required when using a full cluster URI.");
                }
            }

            if (!string.IsNullOrEmpty(adxUri))
            {
                adxUri = adxUri.Replace(".kusto.windows.net", "");
                adxUri = adxUri.Replace("https://", "");
            }

            adxUri = $"https://dataexplorer.azure.com/clusters/{adxUri}/{database}?query={EncodeQuery(query)}";

            var executionTime = $"{queryExecutionTimeInMilliSeconds / 1000.0} secs";
            string displayText;
            if (!string.IsNullOrWhiteSpace(functionName))
            {
                // For function execution
                displayText = $"[Execute in ADX]({adxUri})\n\nExecuted function `{functionName}` against {regionOrClusterUri}{(!string.IsNullOrWhiteSpace(database) ? $"/{database}" : string.Empty)}:\n<details><summary>View KQL Query</summary>\n<pre>\n{query}\n</pre>\n\n</details>\nRows: {count} Execution time: {executionTime}";
            }
            else if (!string.IsNullOrWhiteSpace(database))
            {
                // For cluster and database-specific queries
                displayText = $"[Execute in ADX]({adxUri})\n\nExecuted query on cluster '{regionOrClusterUri}' in database '{database}':\n<details><summary>View KQL Query</summary>\n<pre>\n{query}\n</pre>\n\n</details>\nRows: {count} Execution time: {executionTime}";
            }
            else
            {
                // For regional queries
                displayText = $"[Execute in ADX]({adxUri})\n\nExecuted query in region {regionOrClusterUri}:\n<details><summary>View KQL Query</summary>\n<pre>\n{query}\n</pre>\n\n</details>\nRows:{count} Execution time: {executionTime}";
            }

            return new ChatMessage(ChatRole.Tool, new List<AIContent>
                {
                new UriContent(adxUri, "text/html"),
                new Microsoft.Extensions.AI.TextContent(displayText)
            });
        }

        public static string EncodeQuery(string input)
        {
            byte[] inputBytes = Encoding.UTF8.GetBytes(input);
            using (var outputStream = new MemoryStream())
            {
                using (var gzipStream = new GZipStream(outputStream, CompressionMode.Compress))
                {
                    gzipStream.Write(inputBytes, 0, inputBytes.Length);
                }
                byte[] compressedBytes = outputStream.ToArray();
                string base64 = Convert.ToBase64String(compressedBytes);
                string urlEncoded = HttpUtility.UrlEncode(base64);
                return urlEncoded;
            }
        }

        private KustoQueryResult CreateErrorResult(string query, string errorMessage, string? functionName = null)
        {
            var fullErrorMessage = functionName != null
                ? $"An error occurred while executing Kusto Function {functionName}: {errorMessage}"
                : $"An error occurred while executing Kusto Query: {errorMessage}";

            return new KustoQueryResult()
            {
                Success = false,
                Query = query,
                Result = fullErrorMessage,
                Message = new ChatMessage(ChatRole.Tool, $"<details><summary>View KQL Query</summary>\n<pre>\n{query}\n</pre>\n\n</details>\n\n<strong>{fullErrorMessage}</strong>"),
                RowCount = 0,
            };
        }

        internal static string GetKqlFilePath(string functionName)
        {
            return GetKqlFilePath(functionName, AppContext.BaseDirectory);
        }

        private static string FormatQuery(Dictionary<string, string> args, string fileName)
        {
            var formatted = File.ReadAllText(fileName);
            if (args == null)
            {
                return formatted;
            }
            foreach (var arg in args)
            {
                formatted = formatted.Replace($"##{arg.Key}##", arg.Value);
            }

            if (formatted.Contains("##"))
            {
                throw new Exception($"Not all placeholders were replaced in the query, {formatted}");
            }

            return formatted;
        }

        public static string FormatQuery(string query, Dictionary<string, string> args)
        {
            if (args == null)
            {
                return query;
            }
            foreach (var arg in args)
            {
                query = query.Replace($"##{arg.Key}##", arg.Value);
            }

            if (query.Contains("##"))
            {
                throw new Exception($"Not all placeholders were replaced in the query, {query}");
            }

            return query;
        }

        public async Task<KustoQueryResult> ExecuteClusterKustoQueryInternal(
            string cluster,
            string database,
            string fullQuery)
        {
            cluster = cluster.Replace(".kusto.windows.net", "");
            cluster = cluster.Replace("https://", "");

            var logMessage = $"[execute_kusto_query_on_cluster][{DateTime.UtcNow}] Invoked with cluster: {cluster}, database: {database}\nquery:\n{fullQuery.Substring(0, Math.Min(100, fullQuery.Length))}...";
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var reader = await _kustoClient.PerformQueryAsync($"https://{cluster}.kusto.windows.net", database, fullQuery);
                var result = new KustoQueryResult(reader, fullQuery);
                stopwatch.Stop();
                if (result.Result != null && result.Result != string.Empty)
                {
                    if (result.RowCount == 0 && !result.Result.StartsWith("An error occurred while executing Kusto Query"))
                    {
                        result.Result = "ZERO_ROWS_RETURNED";

                    }
                    return result;
                }
                else
                {
                    _logger.LogInternalInformation($"Kusto query execution failed. Result: {result?.Result}, Message: {result?.Message}");
                    result = CreateErrorResult(fullQuery, "Kusto query execution failed.");
                }
                result.Message = CreateChatMessage(fullQuery, cluster, result.RowCount, (int)stopwatch.ElapsedMilliseconds, string.Empty, database);
                return result;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError($"An error occurred while executing Kusto Query: {ex.Message}");
                return CreateErrorResult(fullQuery, ex.Message);
            }
        }

        public async Task<string> ExecuteLocalFunctionOnClusterAsync(string functionName, string clusterName, string databaseName, Dictionary<string, string> args)
        {
            var fileName = GetKqlFilePath(functionName);
            KustoQueryResult queryResult;
            if (File.Exists(fileName))
            {
                var formatted = FormatQuery(args, fileName);
                queryResult = await ExecuteClusterKustoQueryInternal(clusterName, databaseName, formatted);
            }
            else
            {
                throw new ArgumentException($"Function {functionName} not found in {fileName}");
            }

            if (queryResult.Result.Length > TokenLimit)
            {
                return "Query result row count is over thersholds a user should use sampling";
            }
            var msg = new ChatMessage(ChatRole.Tool, $"`{functionName}`{Environment.NewLine + Environment.NewLine}{queryResult.Message?.Text}");
            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(ToolStatic.AsyncLocalThreadId.Value, string.Empty, msg);

            return queryResult.Result;
        }

        public async Task<string> ExecuteLocalFunctionAsync(string functionName, string region, Dictionary<string, string> args, string? groupName, SamplingOptions? samplingOptions = null)
        {
            region = region.NormalizeLocation();
            SamplingParameterHelper.AddSamplingParameters(args, samplingOptions);
            var fileName = GetKqlFilePath(functionName);
            KustoQueryResult queryResult;

            if (File.Exists(fileName))
            {
                var formatted = FormatQuery(args, fileName);
                queryResult = await ExecuteKustoQueryInternal(region, formatted, groupName ?? string.Empty);
            }
            else
            {
                queryResult = await ExecuteFunctionInternalAsync(functionName, region, args, groupName ?? string.Empty);
            }

            if (queryResult.Result.Length > TokenLimit)
            {
                return "Query result row count is over thersholds a user should use sampling";
            }

            var msg = new ChatMessage(ChatRole.Tool, $"`{functionName}`{Environment.NewLine + Environment.NewLine}{queryResult.Message?.Text}");
            await _agentOutboundCommunicationService.UpdateThreadWithAgentMessageAsync(ToolStatic.AsyncLocalThreadId.Value, string.Empty, msg);

            return queryResult.Result;
        }
    }
}
