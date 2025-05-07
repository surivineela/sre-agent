// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Data;
using System.Text.RegularExpressions;
using Agent.Core.Helpers;
using Agent.Core.Models;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.AI;
using System.Text;
using System.IO.Compression;
using System.Web;
using Newtonsoft.Json;

namespace FirstPartyAgent.Plugins
{
    public partial class KustoPlugin : IKustoPlugin
    {
        private readonly ILogger<KustoPlugin> _logger;
        private readonly KustoClientService _kustoClientService;
        private readonly ITeamsClient _teamsClient;

        [GeneratedRegex("[^a-zA-Z\\d]")]
        private static partial Regex RegionNormalizationRegex();

        public KustoPlugin(ILogger<KustoPlugin> logger, KustoClientService kustoClientService, ITeamsClient teamsClient)
        {
            _teamsClient = teamsClient;
            _logger = logger;
            _kustoClientService = kustoClientService;
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
                _logger.LogInformation($"execute_kusto_query called with {region} / {query}");

                var normalizedRegion = RegionNormalizationRegex().Replace(region, string.Empty).ToLowerInvariant();
                using var reader = await _kustoClientService.PerformQueryAsync(query, region);
                var ret = new KustoQueryResult(reader, query);
                ret.Message = CreateChatMessage(query, normalizedRegion, ret.RowCount);
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
            DateTime? NowOverride,
            Kernel kernel
            )
        {
            cluster = cluster.Replace(".kusto.windows.net", "");
            cluster = cluster.Replace("https://", "");

            var logMessage = $"[execute_kusto_query_on_cluster][{DateTime.UtcNow}] Invoked with cluster: {cluster}, database: {database}\nquery:\n{fullQuery.Substring(0, Math.Min(100, fullQuery.Length))}...";
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            try
            {
                var config = new KustoCluster
                {
                    ClusterUri = $"https://{cluster}.kusto.windows.net",
                    Database = database,
                };
                var reader = await _kustoClientService.PerformQueryAsync(config, fullQuery);
                var ret = new KustoQueryResult(reader, fullQuery);
                ret.Message = CreateChatMessage(fullQuery, cluster, ret.RowCount, database);
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

            using var reader = await _kustoClientService.PerformQueryAsync(query, region);
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
                using var reader = await _kustoClientService.PerformQueryAsync(query, region);
                var ret = new KustoQueryResult(reader, query);
                ret.Message = CreateChatMessage(query, region, ret.RowCount, functionName: functionName);
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

        public ChatMessage CreateChatMessage(string query, string regionOrClusterUri, int count, string? database = null, string? functionName = null)
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
                KustoCluster? cluster = _kustoClientService.GetCluster(region);
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

            string displayText;
            if (!string.IsNullOrWhiteSpace(functionName))
            {
                // For function execution
                displayText = $"[Execute in ADX]({adxUri})\n\nExecuted function `{functionName}` against {regionOrClusterUri}{(!string.IsNullOrWhiteSpace(database)? $"/{database}" : string.Empty)}:\n```kql\n{query}\n```\n\nRows: {count}";
            }
            else if (!string.IsNullOrWhiteSpace(database))
            {
                // For cluster and database-specific queries
                displayText = $"[Execute in ADX]({adxUri})\n\nExecuted query on cluster '{regionOrClusterUri}' in database '{database}':\n```kql\n{query}\n```\n\nRows: {count}";
            }
            else
            {
                // For regional queries
                displayText = $"[Execute in ADX]({adxUri})\n\nExecuted query in region {regionOrClusterUri}:\n```kql\n{query}\n```\n\nRows:{count}";
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
