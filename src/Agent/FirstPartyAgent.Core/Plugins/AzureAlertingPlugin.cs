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

namespace FirstPartyAgent.Core.Plugins
{
    public class AzureAlertingPlugin
    {
        private readonly AzureAlertingClient _azureAlertingClient;
        private readonly ICMPlugin _icmPlugin;
        private readonly IStorageService _storageService;
        private readonly ILogger<AzureAlertingPlugin> _logger;
        private readonly Kernel _kernel;
        private ITeamsClient _teamsClient;
        private IKustoPlugin _kustoPlugin;
        private readonly bool ICMBacktestingModeEnabled = false;

        public AzureAlertingPlugin(ICMWorkflowSettings icmWorkflowSettings, IStorageService storageService, AzureAlertingClient azureAlertingClient, ILogger<AzureAlertingPlugin> logger, ICMPlugin icmPlugin, Kernel kernel, ITeamsClient teamsClient, IKustoPlugin kustoPlugin)
        {
            ICMBacktestingModeEnabled = icmWorkflowSettings.ICMBacktestingModeEnabled;
            _logger = logger;
            _azureAlertingClient = azureAlertingClient;
            _storageService = storageService;
            _icmPlugin = icmPlugin;
            _kernel = kernel;
            _teamsClient = teamsClient;
            _kustoPlugin = kustoPlugin;
        }

        private async Task<AlertDetails> GetAzureAlertingDetailsById(
            string azureAlertingId)
        {
            _logger.LogInformation($"AzureAlertingPlugin: Fetching Alert Details. azureAlertingId: {azureAlertingId}");

            // First check in local folder
            if (!_storageService.IsEnabled)
            {
                try
                {
                    _logger.LogInformation($"AzureAlertingPlugin: Fetching Alert Details from local folder. azureAlertingId: {azureAlertingId}");
                    //Read from local folder called AlertDetails
                    var folderPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "AlertDetails");
                    if (!Directory.Exists(folderPath))
                    {
                        Directory.CreateDirectory(folderPath);
                    }
                    var filePath = Path.Combine(folderPath, $"{azureAlertingId}.json");
                    if (File.Exists(filePath))
                    {
                        var fileContent = File.ReadAllText(filePath);
                        var alertDetails = JsonConvert.DeserializeObject<AlertDetails>(fileContent);
                        return alertDetails;
                    }
                    else
                    {
                        _logger.LogError($"Alert details file not found in local folder: {filePath}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error reading alert details from local folder: {ex.Message}");
                }
            }

            // If not found in local folder, check in storage
            if (_storageService.IsEnabled)
            {
                try
                {
                    _logger.LogInformation($"AzureAlertingPlugin: Fetching Alert Details from Storage. azureAlertingId: {azureAlertingId}");
                    var fileContent = await _storageService.ReadFileFromStorage("alertdetails", $"{azureAlertingId}.json");
                    var alertDetails = JsonConvert.DeserializeObject<AlertDetails>(fileContent);
                    return alertDetails;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error reading alert details from storage: {ex.Message} for azureAlertingId: {azureAlertingId}. Will attempt to read from Azure Alerting.");
                }
            }
            
            //Finally check in Azure Alerting
            if (_azureAlertingClient.IsEnabled())
            {
                try
                {
                    _logger.LogInformation($"AzureAlertingPlugin: Fetching Alert Details from Azure Alerting. azureAlertingId: {azureAlertingId}");
                    var alertDetails = await _azureAlertingClient.GetAlertDetails(azureAlertingId);
                    return alertDetails;
                }
                catch (Exception ex)
                {
                    _logger.LogError($"Error fetching alert details from Azure Alerting: {ex.Message}");
                }
            }
            return null;
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
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            DateTime? nowOverride = null;
            if (ICMBacktestingModeEnabled)
            {
                nowOverride = DateTime.TryParse(impactStartDate, out var impactStartDateTime) ? impactStartDateTime : DateTime.UtcNow;
                nowOverride = nowOverride?.AddSeconds(monitoringIterationNumber * monitoringGapInSeconds);
            }
            var alertDetails = await GetAzureAlertingDetailsById(alertId);
            var alertConfig = AgentFinder.GetICMAlertConfig(alertId);
            if (alertDetails != null)
            {
                var kustoQuery = alertDetails.PrimaryKustoQuery.KustoQuery;
                if (alertConfig.UseCorrelationIdForKustoQuery)
                {
                    kustoQuery = kustoQuery + "\n" +
                        $"| where CorrelationId == '{correlationId}'";
                }
                var kustoResult = await _kustoPlugin.ExecuteClusterKustoQuery(clusterName, databaseName, kustoQuery, NowOverride: nowOverride, kernel);
                return kustoResult;
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
            await kernel.LogInformation(logMessage, _logger, _teamsClient);
            var incidentDetails = await _icmPlugin.GetIncidentInfo(incidentId, _kernel);
            _logger.LogInformation($"AzureAlertingPlugin: Fetching Alert Details. incidentId: {incidentId}, incidenTtile: {incidentDetails.Title}, owningTeam: {incidentDetails.OwningTeam}, monitoringRole: {incidentDetails.MonitoringRole}, monitoringSlice: {incidentDetails.MonitoringSlice}");
            //match incident details with existing alert configs
            if (incidentDetails.MonitoringRole == "AzureAlerting")
            {
                try
                {
                    var alertId = incidentDetails.MonitoringSlice;
                    await kernel.LogInformation($"[get_alert_details_and_custom_instructions][{DateTime.UtcNow}] Fetching alert details for Azure Alerting Id: {alertId}", _logger, _teamsClient);
                    var alertConfig = AgentFinder.GetICMAlertConfig(alertId);
                    if (alertConfig == null) alertConfig = new ICMAlertConfig() { AlertingId = alertId };
                    var alertDetails = await GetAzureAlertingDetailsById(alertId);

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
                    , _logger, _teamsClient);
                var alertConfigs = AgentFinder.GetICMAlertConfigs();
                foreach (var alertId in alertConfigs.Keys)
                {
                    var alertConfig = alertConfigs[alertId];
                    if (incidentDetails.Title == alertConfig.IncidentTitle
                        || (!string.IsNullOrWhiteSpace(alertConfig.IncidentTitleContains) && incidentDetails.Title.Contains(alertConfig.IncidentTitleContains, StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrWhiteSpace(alertConfig.OwningTeam) && incidentDetails.OwningTeam.Equals(alertConfig.OwningTeam, StringComparison.OrdinalIgnoreCase)))
                    {
                        return "PROVIDED_MITIGATION_INSTRUCTIONS:\n" + JsonConvert.SerializeObject(alertConfig);
                    }
                }
            }
            return $"Alert details could not be found for incidentId {incidentId}";
        }
    }
}

