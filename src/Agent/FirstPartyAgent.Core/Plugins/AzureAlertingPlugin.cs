// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Models;
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
        private readonly Kernel _kernel;
        private ITeamsClient _teamsClient;
        private IKustoPlugin _kustoPlugin;
        private ISessionMessageService _sessionMessageService;
        private readonly bool ICMBacktestingModeEnabled = false;

        private readonly AlertHandlerService _alertHandlerService;

        public AzureAlertingPlugin(
            ICMWorkflowSettings icmWorkflowSettings,
            ILogger<AzureAlertingPlugin> logger,
            ICMPlugin icmPlugin,
            Kernel kernel,
            ITeamsClient teamsClient,
            IKustoPlugin kustoPlugin,
            ISessionMessageService sessionMessageService,
            AlertHandlerService alertHandlerService)
        {
            ICMBacktestingModeEnabled = icmWorkflowSettings.ICMBacktestingModeEnabled;
            _logger = logger;
            _icmPlugin = icmPlugin;
            _kernel = kernel;
            _teamsClient = teamsClient;
            _kustoPlugin = kustoPlugin;
            _sessionMessageService = sessionMessageService;
            _alertHandlerService = alertHandlerService;
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
                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, NowOverride: nowOverride, kernel);
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

            var incidentDetails = await _icmPlugin.GetIncidentInfo(incidentId, _kernel);
            _logger.LogInformation($"AzureAlertingPlugin: Fetching Alert Details. incidentId: {incidentId}, incidenTtile: {incidentDetails.Title}, owningTeam: {incidentDetails.OwningTeam}, monitoringRole: {incidentDetails.MonitoringRole}, monitoringSlice: {incidentDetails.MonitoringSlice}");
            //match incident details with existing alert configs
            if (incidentDetails.MonitoringRole == "AzureAlerting" || incidentDetails.CreatedBy == "AzureAlerting")
            {
                try
                {
                    string alertId = incidentDetails.MonitoringSlice;
                    await kernel.LogInformation($"[get_alert_details_and_custom_instructions][{DateTime.UtcNow}] Fetching alert details for Azure Alerting Id: {alertId}", _logger, _teamsClient, _sessionMessageService);
                    var alertConfig = alertId == customAlertConfig?.AlertingId ? customAlertConfig : await _alertHandlerService.GetICMAlertConfigAsync(alertId);
                    if (alertConfig == null) alertConfig = new ICMAlertConfig() { AlertingId = alertId };
                    // var alertDetails = await _alertHandlerService.GetAzureAlertingDetailsById(alertId);

                    if (alertConfig != null)
                    {
                        return $"ALERT_ID: {alertId}" +
                        "\n\n" +
                        "PROVIDED_MITIGATION_INSTRUCTIONS:\n" + JsonConvert.SerializeObject(alertConfig);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error fetching alert details from Azure Alerting: {ex.Message}");
                }
            }
            else
            {
                await kernel.LogInformation($"[get_alert_details_and_custom_instructions][{DateTime.UtcNow}] AzureAlertingPlugin: This Incident is not from Azure Alerting, finding configuration based on other fields."
                    , _logger, _teamsClient, _sessionMessageService);
                var alertConfigs = await _alertHandlerService.GetICMAlertConfigsAsync();
                foreach (var alertId in alertConfigs.Keys)
                {
                    var alertConfig = alertConfigs[alertId];
                    if (incidentDetails.Title == alertConfig.IncidentTitle
                        || (!string.IsNullOrWhiteSpace(alertConfig.IncidentTitleContains) && incidentDetails.Title.Contains(alertConfig.IncidentTitleContains, StringComparison.OrdinalIgnoreCase))
                        || (alertConfig.OwningTeams != null && alertConfig.OwningTeams.Count > 0 && alertConfig.OwningTeams.Any(x => x.Equals(incidentDetails.OwningTeam, StringComparison.OrdinalIgnoreCase))))
                    {
                        return "PROVIDED_MITIGATION_INSTRUCTIONS:\n" + JsonConvert.SerializeObject(alertConfig);
                    }
                }
            }
            return $"Alert details could not be found for incidentId {incidentId}";
        }
    }
}

