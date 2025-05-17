using FirstPartyAgent.Core.Constants;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Helpers;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Newtonsoft.Json;
using System.ComponentModel;

namespace FirstPartyAgent.Core.Helpers;

public class AlertHandlerClient
{
    private readonly ILogger<AlertHandlerClient> _logger;
    private readonly ITeamsClient _teamsClient;
    private readonly ISessionMessageService _sessionMessageService;
    private readonly AlertHandlerService _alertHandlerService;

    public AlertHandlerClient(ILogger<AlertHandlerClient> logger, ITeamsClient teamsClient, ISessionMessageService sessionMessageService, AlertHandlerService alertHandlerService)
    {
        _logger = logger;
        _teamsClient = teamsClient;
        _sessionMessageService = sessionMessageService;
        _alertHandlerService = alertHandlerService;
    }

    public async Task<ICMAlertConfig> GetConfigAsync(Incident incidentDetails, Kernel kernel)
    {
        if (kernel.Data.ContainsKey("alertConfig"))
        {
            return (ICMAlertConfig)kernel.Data["alertConfig"];
        }

        ICMAlertConfig alertConfig = null;
        var customAlertConfig = kernel.Data.TryGetValue("customAlertConfig", out object customAlertConfigObj) ? (ICMAlertConfig)customAlertConfigObj : null;

        if ((incidentDetails.MonitoringRole == "AzureAlerting" || incidentDetails.CreatedBy == "AzureAlerting") && incidentDetails.MonitoringSlice != null)
        {
            try
            {
                string alertId = incidentDetails.MonitoringSlice;
                await kernel.LogInformation($"[get_alert_details_and_custom_instructions][{DateTime.UtcNow}] Fetching alert details for Azure Alerting Id: {alertId}", _logger, _teamsClient, _sessionMessageService);
                alertConfig = alertId == customAlertConfig?.AlertingId ? customAlertConfig : await _alertHandlerService.GetICMAlertConfigAsync(alertId);
                if (alertConfig == null) alertConfig = new ICMAlertConfig() { AlertingId = alertId };
                var alertDetails = await _alertHandlerService.GetAzureAlertingDetailsById(alertId);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error fetching alert details from Azure Alerting: {ex.Message}");
            }
        }
        if (alertConfig == null)
        {
            await kernel.LogInformation($"[get_alert_details_and_custom_instructions][{DateTime.UtcNow}] AzureAlertingPlugin: This Incident is not from Azure Alerting, finding configuration based on other fields."
                , _logger, _teamsClient, _sessionMessageService);
            var alertConfigs = await _alertHandlerService.GetICMAlertConfigsAsync();
            foreach (var alertId in alertConfigs.Keys)
            {
                var testAlertConfig = alertConfigs[alertId];
                if (incidentDetails.Title == testAlertConfig.IncidentTitle
                    || (!string.IsNullOrWhiteSpace(testAlertConfig.IncidentTitleContains) && incidentDetails.Title.Contains(testAlertConfig.IncidentTitleContains, StringComparison.OrdinalIgnoreCase))
                    || (testAlertConfig.OwningTeams != null && testAlertConfig.OwningTeams.Count > 0 && testAlertConfig.OwningTeams.Any(x => x.Equals(incidentDetails.OwningTeam, StringComparison.OrdinalIgnoreCase))))
                {
                    return testAlertConfig;
                }
            }
        }
        if (alertConfig != null)
        {
            kernel.Data["alertConfig"] = alertConfig;
        }
        return alertConfig;
    }
}
