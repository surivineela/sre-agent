// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Data;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Agent.Core.Models;
using FirstPartyAgent.Core.Clients;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
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

    public partial class KustoPlugin : IKustoPlugin
    {
        private readonly ILogger<KustoPlugin> _logger;
        private readonly KustoClient _kustoClient;
        private readonly ITeamsClient _teamsClient;
        private readonly KustoRegionalGroupClientProvider _kustoRegionalGroupClientProvider;

        [GeneratedRegex("[^a-zA-Z\\d]")]
        private static partial Regex RegionNormalizationRegex();

        public KustoPlugin(ILogger<KustoPlugin> logger, KustoRegionalGroupClientProvider kustoRegionalGroupClientProvider, KustoClient kustoClient, ITeamsClient teamsClient)
        {
            _teamsClient = teamsClient;
            _logger = logger;
            _kustoClient = kustoClient;
            _kustoRegionalGroupClientProvider = kustoRegionalGroupClientProvider;
        }

        [KernelFunction("execute_kusto_query")]
        [Description("Executes a Kusto query on a regional cluster and returns the result as a JSON string.")]
        public async Task<KustoQueryResult> ExecuteKustoQuery(
            [Description("The region of the target Kusto cluster.")] string region,
            [Description("The Kusto query to execute.")] string query
            )
        {
            try
            {
                // TODO: update this plugin with a parameter to allow querying regional clusters for other products
                KustoRegionalGroupClient regionalKustoClient = _kustoRegionalGroupClientProvider.GetRegionalGroupKustoClient("ContainerApps");

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                _logger.LogInformation($"execute_kusto_query called with {region} / {query}");

                var normalizedRegion = RegionNormalizationRegex().Replace(region, string.Empty).ToLowerInvariant();
                using var reader = await regionalKustoClient.PerformQueryAsync(query, region);

                stopwatch.Stop();
                var ret = new KustoQueryResult(reader, query);
                ret.Message = CreateChatMessage(query, normalizedRegion, ret.RowCount, (int)stopwatch.ElapsedMilliseconds);

                return ret;
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while executing Kusto Query: {ex.Message}");
                return new KustoQueryResult()
                {
                    Query = query,
                    Result = $"An error occurred while executing Kusto Query: {ex.Message}",
                    RowCount = 0,
                };
            }
        }

        [KernelFunction("execute_kusto_query_on_cluster")]
        [Description("Executes a fully qualified Kusto query on a specific cluster and database, returning the result in JSON format.")]
        public async Task<KustoQueryResult> ExecuteClusterKustoQuery(
            [Description("The short name of the target Kusto cluster (without URL schema or suffix).")] string cluster,
            [Description("The name of the target Kusto database.")] string database,
            [Description("The full Kusto query to execute.")] string fullQuery,
            DateTime? NowOverride
            )
        {
            cluster = cluster.Replace(".kusto.windows.net", "");
            cluster = cluster.Replace("https://", "");

            var logMessage = $"[execute_kusto_query_on_cluster][{DateTime.UtcNow}] Invoked with cluster: {cluster}, database: {database}\nquery:\n{fullQuery.Substring(0, Math.Min(100, fullQuery.Length))}...";
            try
            {
                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                var reader = await _kustoClient.PerformQueryAsync($"https://{cluster}.kusto.windows.net", database, fullQuery);
                var ret = new KustoQueryResult(reader, fullQuery);
                stopwatch.Stop();
                ret.Message = CreateChatMessage(fullQuery, cluster, ret.RowCount, (int)stopwatch.ElapsedMilliseconds, database:database);
                return ret;
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while executing Kusto Query: {ex.Message}");
                return new KustoQueryResult()
                {
                    Query = fullQuery,
                    Result = $"An error occurred while executing Kusto Query: {ex.Message}",
                    RowCount = 0,
                };
            }
        }

        [KernelFunction("list_kusto_functions")]
        [Description("Lists all available user-defined functions from the Kusto cluster for a given region, including metadata like name, folder, and description.")]
        public async Task<List<KustoFunction>> ListFunctionsAsync(
            [Description("The region of the Kusto cluster to query.")] string region)
        {
            var query = ".show functions | project Name, Folder, DocString, Parameters";
            var result = new List<KustoFunction>();

            // TODO: update this plugin with a parameter to allow querying regional clusters for other products
            KustoRegionalGroupClient regionalKustoClient = _kustoRegionalGroupClientProvider.GetRegionalGroupKustoClient("ContainerApps");

            using var reader = await regionalKustoClient.PerformQueryAsync(query, region);
            while (reader.Read())
            {
                result.Add(new KustoFunction
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
        public async Task<KustoQueryResult> ExecuteFunctionAsync(
            [Description("The name of the Kusto function to invoke.")] string functionName,
            [Description("The region of the Kusto cluster.")] string region,
            Dictionary<string, string>? args = null
            )
        {
            string argList = args != null && args.Count > 0
                ? string.Join(", ", args.Select(kvp => $"{kvp.Key}={QuoteIfNeeded(kvp.Value)}"))
                : "";

            var query = string.IsNullOrEmpty(argList) ? $"{functionName}()" : $"{functionName}({argList})";

            try
            {
                // TODO: update this plugin with a parameter to allow querying regional clusters for other products
                KustoRegionalGroupClient regionalKustoClient = _kustoRegionalGroupClientProvider.GetRegionalGroupKustoClient("ContainerApps");

                var stopwatch = System.Diagnostics.Stopwatch.StartNew();
                using var reader = await regionalKustoClient.PerformQueryAsync(query, region);
                var ret = new KustoQueryResult(reader, query);
                stopwatch.Stop();
                ret.Message = CreateChatMessage(query, region, ret.RowCount, (int)stopwatch.ElapsedMilliseconds, functionName: functionName);
                return ret;
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while executing Kusto Function {functionName}: {ex.Message}");
                return new KustoQueryResult()
                {
                    Query = query,
                    Result = $"An error occurred while executing Kusto Function {functionName}: {ex.Message}",
                    RowCount = 0,
                };
            }
        }

        private static string QuoteIfNeeded(string value)
        {
            return $"\"{value}\"";
        }

        public ChatMessage CreateChatMessage(string query, string regionOrClusterUri, int count, int queryExecutionTimeInMilliSeconds, string? database = null, string? functionName = null)
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
                    KustoRegionalGroupClient regionalKustoClient = _kustoRegionalGroupClientProvider.GetRegionalGroupKustoClient("ContainerApps");
                    cluster = regionalKustoClient.GetCluster(region);
                } catch (Exception ex)
                {
                    _logger.LogError($"An error occurred while getting Kusto cluster for region {region}: {ex.Message}");
                }

                if (cluster != null)
                {
                    adxUri = cluster.ClusterUri;
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

            adxUri = adxUri.Replace(".kusto.windows.net", "");
            adxUri = adxUri.Replace("https://", "");

            adxUri = $"https://dataexplorer.azure.com/clusters/{adxUri}/{database}?query={EncodeQuery(query)}";

            var executionTime = $"{queryExecutionTimeInMilliSeconds / 1000.0} secs";
            string displayText;
            if (!string.IsNullOrWhiteSpace(functionName))
            {
                // For function execution
                displayText = $"[Execute in ADX]({adxUri})\n\nExecuted function `{functionName}` against {regionOrClusterUri}{(!string.IsNullOrWhiteSpace(database)? $"/{database}" : string.Empty)}:\n```kql\n{query}\n```\n\nRows: {count} Execution time: {executionTime}";
            }
            else if (!string.IsNullOrWhiteSpace(database))
            {
                // For cluster and database-specific queries
                displayText = $"[Execute in ADX]({adxUri})\n\nExecuted query on cluster '{regionOrClusterUri}' in database '{database}':\n```kql\n{query}\n```\n\nRows: {count} Execution time: {executionTime}";
            }
            else
            {
                // For regional queries
                displayText = $"[Execute in ADX]({adxUri})\n\nExecuted query in region {regionOrClusterUri}:\n```kql\n{query}\n```\n\nRows:{count} Execution time: {executionTime}";
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
    }
}
