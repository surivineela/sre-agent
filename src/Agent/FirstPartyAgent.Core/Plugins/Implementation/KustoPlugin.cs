// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text;
using System.Text.RegularExpressions;
using Agent.Core.Helpers;
using Agent.Core.Models;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using Kusto.Cloud.Platform.Data;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

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
        public async Task<string> ExecuteKustoQuery(
            [Description("The region of the target Kusto cluster.")] string region,
            [Description("The Kusto query to execute.")] string query)
        {
            try
            {
                _logger.LogInformation($"execute_kusto_query called with {region} / {query}");
                var normalizedRegion = RegionNormalizationRegex().Replace(region, string.Empty).ToLowerInvariant();
                var reader = await _kustoClientService.PerformQueryAsync(query, region);
                var writer = new StringWriter();

                reader.WriteAsJson(writer, 1024 * 1024, out var size);

                _logger.LogInformation($"result: {writer.ToString()}");
                return writer.ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while executing Kusto Query: {ex.Message}");
                return $"An error occurred while executing Kusto Query: {ex.Message}";
            }
        }

        [KernelFunction("execute_kusto_query_on_cluster")]
        [Description("Executes a fully qualified Kusto query on a specific cluster and database, returning the result in JSON format.")]
        public async Task<string> ExecuteClusterKustoQuery(
            [Description("The short name of the target Kusto cluster (without URL schema or suffix).")] string cluster,
            [Description("The name of the target Kusto database.")] string database,
            [Description("The full Kusto query to execute.")] string fullQuery,
            DateTime? NowOverride,
            Kernel kernel)
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
                var writer = new StringWriter();

                reader.WriteAsJson(writer, 1024 * 1024, out var size);
                if (size > 1024 * 1024)
                {
                    _logger.LogWarning($"Kusto query result size exceeds 1MB: {size} bytes");
                }
                var result = writer.ToString();
                _logger.LogInformation($"Kusto Output: {result}");

                return !string.IsNullOrWhiteSpace(result) ? result : "ZERO_ROWS_RETURNED";
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while executing Kusto Query: {ex.Message}");
                return $"An error occurred while executing Kusto Query: {ex.Message}";
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
                    Name = reader["Name"].ToString(),
                    Folder = reader["Folder"]?.ToString(),
                    DocString = reader["DocString"]?.ToString(),
                    Parameters = reader["Parameters"]?.ToString()
                });
            }

            return result;
        }

        [KernelFunction("execute_kusto_function")]
        [Description("Executes a user-defined Kusto function with named arguments on the regional Kusto cluster and returns the results.")]
        public async Task<string> ExecuteFunctionAsync(
            [Description("The name of the Kusto function to invoke.")] string functionName,
            [Description("The region of the Kusto cluster.")] string region,
            Dictionary<string, string>? args = null)
        {
            string argList = args != null && args.Count > 0
                ? string.Join(", ", args.Select(kvp => $"{kvp.Key}={QuoteIfNeeded(kvp.Value)}"))
                : "";

            var query = string.IsNullOrEmpty(argList) ? $"{functionName}()" : $"{functionName}({argList})";
            using var reader = await _kustoClientService.PerformQueryAsync(query, region);

            var output = new StringBuilder();
            while (reader.Read())
            {
                output.AppendLine(reader[0].ToString());
            }

            return output.ToString();
        }

        private static string QuoteIfNeeded(string value)
        {
            return  $"\"{value}\"" ;
        }
    }
}
