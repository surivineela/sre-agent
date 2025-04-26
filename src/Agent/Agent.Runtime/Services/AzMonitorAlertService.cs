// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Azure.Core;
using Azure.ResourceManager.AlertsManagement.Models;
using Azure.ResourceManager.AlertsManagement;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services;
public class AzMonitorAlertService : IAzMonitorAlertService
{
    private readonly ILogger<AzMonitorAlertService> _logger;
    private readonly IArmClientFactory _armClientFactory;
    private readonly ArmClient _armClient;
    public AzMonitorAlertService(
        IArmClientFactory armClientFactory,
        ILogger<AzMonitorAlertService> logger)
    {
        _armClientFactory = armClientFactory;
        _armClient = _armClientFactory.GetArmClient();
        _logger = logger; 
    }

    public async Task<IEnumerable<ServiceAlertResource>> PollNewAlertsBySubscriptionId(string subscriptionId, int scanWindowInMins = 1)
    {
        var newAlerts = new List<ServiceAlertResource>();

        var subResource = _armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

        ServiceAlertCollection alertCollection = subResource.GetServiceAlerts();

        await foreach (var alertRes in alertCollection.GetAllAsync())
        {
            var data = alertRes.Data;

            var essentials = alertRes.Data.Properties.Essentials;

            if (essentials.MonitorCondition == MonitorCondition.Fired && essentials.StartOn >= DateTime.Now.AddMinutes(-1)) // only get alerts from the last minute
            {
                newAlerts.Add(alertRes);
            }
        }

        return newAlerts;
    }

    public async Task<bool> AcknowledgeAlert(string alertId)
    {
        return await UpdateAlertStatus(alertId, ServiceAlertState.Acknowledged);
    }

    public async Task<bool> UpdateAlertStatus(string alertId, ServiceAlertState alertState)
    {
        try
        {
            var alertResourceIdentifier = new ResourceIdentifier(alertId);
            var alertResource = _armClient.GetServiceAlertResource(alertResourceIdentifier);
            
            var response = await alertResource.ChangeStateAsync(
                alertState,
                "Alert closed by Agent.Runtime");
            
            _logger.LogInformation($"Alert {alertId} status updated to {alertState} successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to update alert status for {alertId}: {ex.Message}");
            return false;
        }
    }
}
