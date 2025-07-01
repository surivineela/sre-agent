// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Configuration;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Models;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Linq;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using Agent.Plugins.Kusto;
using Agent.Core.Extensions;

namespace Agent.Plugins.Implementation
{
    public class AzureAlertingPlugin : IAzureAlertingPlugin
    {
        private readonly IKustoPluginClient _kustoClient;
        private readonly ILogger<AzureAlertingPlugin> _logger;
        private readonly CosmosClient _cosmosDBService;
        private readonly CosmosDBSettings _cosmosDBSettings;
        private readonly Dictionary<string, ICMAlertConfig> _icmAlertConfigs = new(StringComparer.OrdinalIgnoreCase);
        private readonly string _icmAgentConfigCosmosDbContainer = "IcmAlertConfigs";
        private const string _icmAgentAlertDetailsCosmosDbContainer = "IcmAlertDetails";

        public AzureAlertingPlugin(
            IKustoPluginClient kustoClient,
            ILogger<AzureAlertingPlugin> logger,
            CosmosClient cosmosDBService,
            CosmosDBSettings cosmosDBSettings)
        {
            _kustoClient = kustoClient;
            _logger = logger;
            _cosmosDBService = cosmosDBService;
            _cosmosDBSettings = cosmosDBSettings;
        }

        public async Task<string> RunAlertKustoQuery(
             string impactStartDate,
             int monitoringIterationNumber,
             int monitoringGapInSeconds,
             string correlationId,
             string incidentTitle,
             string clusterName,
             string databaseName,
             bool useCorrelationIdForKustoQuery)
        {
            var logMessage = $"[run_alert_kusto_query][{DateTime.UtcNow}] Invoked with impactStartDate: {impactStartDate}, monitoringIterationNumber: {monitoringIterationNumber}, monitoringGapInSeconds: {monitoringGapInSeconds}, correlationId: {correlationId}, clusterName: {clusterName}, databaseName: {databaseName}.";
            _logger.LogInternalInformation(logMessage);

            var alertDetails = await GetAzureAlertingDetailsByTitle(incidentTitle);

            if (alertDetails != null)
            {
                var kustoQuery = alertDetails.PrimaryKustoQuery.KustoQuery;
                if (useCorrelationIdForKustoQuery)
                {
                    kustoQuery = kustoQuery + "\n" +
                        $"| where CorrelationId == '{correlationId}'";
                }
                var kustoResult = await _kustoClient.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery);
                return kustoResult.Result;
            }

            return $"Alert details not found for alert {incidentTitle}";
        }

        private async Task<AlertDetailsBase> GetAzureAlertingDetailsByTitle(string incidentTitle)
        {
            if(incidentTitle.StartsWith("[Public] "))
            {
                incidentTitle = incidentTitle.Substring(9);
            }
            _logger.LogInternalInformation($"AzureAlertingPlugin: Fetching Alert Details. incidentTitle: {incidentTitle}");

            if (_cosmosDBService != null)
            {
                _logger.LogInternalInformation($"AzureAlertingPlugin: Fetching Alert Details from CosmosDB. incidentTitle: {incidentTitle}");
                try
                {
                    var alertDetails = await _cosmosDBService
                        .GetContainer(_cosmosDBSettings.Docs.Database, _icmAgentAlertDetailsCosmosDbContainer)
                        .GetItemLinqQueryable<AlertDetailsBase>(true)
                        .Where(a => a.Title == incidentTitle)
                        .ToListAsync();

                    if (alertDetails != null && alertDetails.Count > 0)
                    {
                        return alertDetails.FirstOrDefault();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError($"Error reading alert details from CosmosDB: {ex.Message}");
                }
            }

            return null;
        }

    }
}
