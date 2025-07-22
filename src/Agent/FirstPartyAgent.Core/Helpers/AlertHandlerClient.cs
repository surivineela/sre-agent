using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

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

    public async Task<ICMAlertConfig?> GetConfigAsync(Incident incidentDetails, Kernel kernel)
    {
        if (kernel.Data.ContainsKey("alertConfig"))
        {
            return (ICMAlertConfig?)kernel.Data["alertConfig"];
        }

        ICMAlertConfig? alertConfig = null;
        var customAlertConfig = kernel.Data.TryGetValue("customAlertConfig", out object? customAlertConfigObj) ? (ICMAlertConfig?)customAlertConfigObj : null;

        string agentMode = string.Empty;
        if(kernel.Data.TryGetValue("agentMode", out var value)) {
            agentMode = (string)(value ?? "");
        }

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
                    || (testAlertConfig.OwningTeams != null && testAlertConfig.OwningTeams.Count > 0 && testAlertConfig.OwningTeams.Any(x => x.Equals(incidentDetails.OwningTeam, StringComparison.OrdinalIgnoreCase)))
                    || (testAlertConfig.MonitorId != null && testAlertConfig.MonitorId.Equals(incidentDetails.MonitorId, StringComparison.OrdinalIgnoreCase))
                    || IsEmergingIssue(agentMode, testAlertConfig, incidentDetails)
                    )
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

    /// <summary>
    /// Determines if the incident should be treated as an emerging issue
    /// based on the agent mode and incident team ownership
    /// </summary>
    /// <param name="agentMode">The current agent mode</param>
    /// <param name="config">The alert configuration to check against</param>
    /// <param name="incident">The incident to evaluate</param>
    /// <returns>True if the incident should be treated as an emerging issue, false otherwise</returns>
    private bool IsEmergingIssue(string agentMode, ICMAlertConfig config, Incident incident)
    {
        // Quick exit if not in EmergingIssue mode
        if (!string.Equals("EmergingIssue", agentMode, StringComparison.InvariantCultureIgnoreCase))
        {
            return false;
        }

        // Get tags from config, defaulting to empty list if null
        var configTags = config.Tags ?? new List<string>();
        
        // Check for "Functions" tag and related team ownership
        if (configTags.Any(tag => string.Equals("Functions", tag, StringComparison.InvariantCultureIgnoreCase)))
        {
            // Check if any owning team contains "AntaresFunctions" or "Antares Functions
            var team = incident?.OwningTeam ?? "";
            return team.Contains("AntaresFunctions", StringComparison.OrdinalIgnoreCase) ||
                   team.Contains("Antares Functions", StringComparison.OrdinalIgnoreCase) ||
                   team.Contains("WebAppsFunctions", StringComparison.OrdinalIgnoreCase); 
        }
        
        return false;
    }
}
