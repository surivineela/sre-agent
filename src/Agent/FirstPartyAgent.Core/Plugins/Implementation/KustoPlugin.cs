// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Models;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using Kusto.Cloud.Platform.Data;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using System;
using System.ComponentModel;
using System.Text.RegularExpressions;

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
        [Description("[DEPRECATED] - Execute a query on the regional kusto cluster and returns JSON response")]
        public async Task<string> ExecuteKustoQuery(
            [Description("The region of the target kusto")] string region,
            [Description("The query to execute")] string query)
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
        [Description("Executes a fully qualified Kusto query on a cluster and returns JSON response")]
        public async Task<string> ExecuteClusterKustoQuery(
            [Description("The name (only) of the Kusto cluster")] string cluster,
            [Description("The name of the Kusto database")] string database,
            [Description("The full kusto query to execute")] string fullQuery,
            DateTime? NowOverride,
            Kernel kernel)
        {
            cluster = cluster.Replace(".kusto.windows.net", "");
            var logMessage = $"[execute_kusto_query_on_cluster][{DateTime.UtcNow}] Invoked with cluster: {cluster}, database: {database}\nquery:\n{fullQuery.Substring(0, 100)}...";
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

                if (!string.IsNullOrWhiteSpace(result))
                {
                    return result;
                }
                else
                {
                    return "ZERO_ROWS_RETURNED";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while executing Kusto Query: {ex.Message}");
                return $"An error occurred while executing Kusto Query: {ex.Message}";
            }
        }
    }
}
