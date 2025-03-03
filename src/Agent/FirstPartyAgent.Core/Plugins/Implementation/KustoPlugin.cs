// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Kusto.Cloud.Platform.Data;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins
{
    public partial class KustoPlugin : IKustoPlugin
    {
        private readonly ILogger<KustoPlugin> _logger;
        private readonly KustoClientService _kustoClientService;

        [GeneratedRegex("[^a-zA-Z\\d]")]
        private static partial Regex RegionNormalizationRegex();

        public KustoPlugin(ILogger<KustoPlugin> logger, KustoClientService kustoClientService)
        {
            _logger = logger;
            _kustoClientService = kustoClientService;
        }

        [KernelFunction("execute_kusto_query")]
        [Description("Execute a query on the regional kusto cluster and returns JSON response")]
        public async Task<string> ExecuteKustoQuery(
            [Description("The region of the target kusto")] string region,
            [Description("The query to execute")] string query)
        {
            _logger.LogInformation($"execute_kusto_query called with {region} / {query}");
            var normalizedRegion = RegionNormalizationRegex().Replace(region, string.Empty).ToLowerInvariant();
            var reader = await _kustoClientService.PerformQueryAsync(query, region);
            var writer = new StringWriter();

            reader.WriteAsJson(writer, 1024 * 1024, out var size);

            _logger.LogInformation($"result: {writer.ToString()}");
            return writer.ToString();
        }

        [KernelFunction("execute_kusto_query_on_cluster")]
        [Description("Executes a fully qualified Kusto query on a cluster and returns JSON response")]
        public async Task<string> ExecuteClusterKustoQuery(string cluster, string database, string fullQuery)
        {
            var config = new KustoCluster
            {
                ClusterUri = $"https://{cluster}.kusto.windows.net",
                Database = database,
            };
            var reader = await _kustoClientService.PerformQueryAsync(config, fullQuery);
            var writer = new StringWriter();

            reader.WriteAsJson(writer, 1024 * 1024, out var size);
            return writer.ToString();
        }
    }
}
