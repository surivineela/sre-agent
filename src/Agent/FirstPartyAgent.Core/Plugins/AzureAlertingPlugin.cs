// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Linq;

namespace FirstPartyAgent.Core.Plugins
{
    public class AzureAlertingPlugin
    {
        private readonly ICMPlugin _icmPlugin;
        private readonly ILogger<AzureAlertingPlugin> _logger;
        private ITeamsClient _teamsClient;
        private IKustoPluginClient _kustoPlugin;
        private ISessionMessageService _sessionMessageService;
        private readonly bool ICMBacktestingModeEnabled = false;

        private readonly AlertHandlerService _alertHandlerService;
        private readonly AlertHandlerClient _alertHandlerClient;

        public AzureAlertingPlugin(
            ICMWorkflowSettings icmWorkflowSettings,
            ILogger<AzureAlertingPlugin> logger,
            ICMPlugin icmPlugin,
            ITeamsClient teamsClient,
            IKustoPluginClient kustoPlugin,
            ISessionMessageService sessionMessageService,
            AlertHandlerService alertHandlerService,
            AlertHandlerClient alertHandlerClient)
        {
            ICMBacktestingModeEnabled = icmWorkflowSettings.ICMBacktestingModeEnabled;
            _logger = logger;
            _icmPlugin = icmPlugin;
            _teamsClient = teamsClient;
            _kustoPlugin = kustoPlugin;
            _sessionMessageService = sessionMessageService;
            _alertHandlerService = alertHandlerService;
            _alertHandlerClient = alertHandlerClient;
        }

        [KernelFunction("run_alert_kusto_query")]
        [Description("Runs the kusto query for the alert and returns the result.")]
        public async Task<string> RunAlertKustoQuery(
            [Description("ImpactStartDate from the Incident Details")] string impactStartDate,
            [Description("Monitoring Iteration Number")] int monitoringIterationNumber,
            [Description("Monitoring Gap In Seconds")] int monitoringGapInSeconds,
            [Description("Correlation Id")] string correlationId,
            [Description("Alert Id")] string alertId,
            [Description("Kusto Cluster Name")] string clusterName,
            [Description("Kusto Database Name")] string databaseName,
            Kernel kernel)
        {
            var logMessage = $"[run_alert_kusto_query][{DateTime.UtcNow}] Invoked with ICMBacktestingModeEnabled: {ICMBacktestingModeEnabled}, impactStartDate: {impactStartDate}, monitoringIterationNumber: {monitoringIterationNumber},  monitoringGapInSeconds: {monitoringGapInSeconds}, correlationId: {correlationId}, alertId: {alertId}, clusterName: {clusterName}, databaseName: {databaseName}.";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            var customAlertConfig = kernel.Data.TryGetValue("customAlertConfig", out object customAlertConfigObj) ? (ICMAlertConfig)customAlertConfigObj : null;
            DateTime? nowOverride = null;
            if (ICMBacktestingModeEnabled)
            {
                nowOverride = DateTime.TryParse(impactStartDate, out var impactStartDateTime) ? impactStartDateTime : DateTime.UtcNow;
                nowOverride = nowOverride?.AddSeconds(monitoringIterationNumber * monitoringGapInSeconds);
            }
            var alertDetails = await _alertHandlerService.GetAzureAlertingDetailsById(alertId);
            var alertConfig = alertId == customAlertConfig?.AlertingId ? customAlertConfig : await _alertHandlerService.GetICMAlertConfigAsync(alertId);
            if (alertDetails != null)
            {
                var kustoQuery = alertDetails.PrimaryKustoQuery.KustoQuery;
                if (alertConfig.UseCorrelationIdForKustoQuery)
                {
                    kustoQuery = kustoQuery + "\n" +
                        $"| where CorrelationId == '{correlationId}'";
                }
                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, NowOverride: nowOverride);
                return kustoResult.Result;
            }
            return $"Alert details not found for alertId {alertId}";
        }


        [KernelFunction("get_alert_details_and_custom_instructions")]
        [Description("Fetches the alert details and custom instructions for an incident. These details involve the kusto queries that are used to check the incident impact and mitigation instructions that must be followed to handle the incident.")]
        public async Task<string> GetAlertDetailsAndCustomInstructions(
            [Description("Incident Id")] string incidentId,
            Kernel kernel)
        {
            var logMessage = $"[get_alert_details_and_custom_instructions][{DateTime.UtcNow}] Invoked with incidentId: {incidentId}.";
            await kernel.LogInformation(logMessage, _logger, _teamsClient, _sessionMessageService);
            string sessionId = kernel.Data.TryGetValue("sessionId", out object id) ? (string)id : null;
            var customAlertConfig = kernel.Data.TryGetValue("customAlertConfig", out object customAlertConfigObj) ? (ICMAlertConfig)customAlertConfigObj : null;

            var incidentDetails = await _icmPlugin.GetIncidentInfo(incidentId, kernel);
            //match incident details with existing alert configs
            ICMAlertConfig alertConfig = await _alertHandlerClient.GetConfigAsync(incidentDetails, kernel);
            _logger.LogInformation($"AzureAlertingPlugin: Fetching Alert Details. incidentId: {incidentId}, incidenTtile: {incidentDetails.Title}, owningTeam: {incidentDetails.OwningTeam}, monitoringRole: {incidentDetails.MonitoringRole}, monitoringSlice: {incidentDetails.MonitoringSlice}");
            
            if (alertConfig != null && incidentDetails.MonitoringSlice != null)
            {
                return $"ALERT_ID: {incidentDetails.MonitoringSlice}" +
                        "\n\n" +
                        "PROVIDED_MITIGATION_INSTRUCTIONS:\n" + JsonConvert.SerializeObject(alertConfig);
            }

            else if (alertConfig != null)
            {
                return "PROVIDED_MITIGATION_INSTRUCTIONS:\n" + JsonConvert.SerializeObject(alertConfig);
            }

            return $"Alert details could not be found for incidentId {incidentId}";
        }
    }
}

