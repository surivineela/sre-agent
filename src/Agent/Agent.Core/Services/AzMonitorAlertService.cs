// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using Agent.Core.Interfaces;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.AlertsManagement.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;
public class AzMonitorAlertService : IAzMonitorAlertService
{
    private readonly ILogger<AzMonitorAlertService> _logger;
    private readonly IAuthenticationService _authService;
    private readonly HttpClient _httpClient;
    private readonly ArmClient _armClient;

    public AzMonitorAlertService(
        IArmClientFactory armClientFactory,
        IAuthenticationService authService,
        ILogger<AzMonitorAlertService> logger)
    {
        _authService = authService;
        _armClient = armClientFactory.GetArmClient();
        _logger = logger;
        _httpClient = new HttpClient();
    }

    public async Task<IEnumerable<AlertItem>> PollNewAlertsBySubscriptionId(string subscriptionId, int scanWindowInMins = 1)
    {
        var newAlerts = new List<AlertItem>();

        try
        {
            _logger.LogInformation($"Getting token for Azure ARM operations for subscription {subscriptionId}");
            // Get the access token for ARM operations
            var credential = _authService.GetArmReadOperationCredential();
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://management.azure.com/.default" }),
                CancellationToken.None);

            var cutoffTime = DateTimeOffset.Now.AddMinutes(-scanWindowInMins);

            // using 2019-05-05-preview API version
            string apiUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.AlertsManagement/alerts?api-version=2019-05-05-preview";

            // only get the alerts for the last 1hour - then filter in memory
            string timeRange = "1h";

            // Add both time range and monitor condition filters
            apiUrl += $"&timeRange={timeRange}&monitorCondition=Fired";

            _logger.LogInformation($"Calling Alert Management API with URL: {apiUrl}");

            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"Response content: {content}");

                var alertResponse = JsonSerializer.Deserialize<AlertsResponse>(content);

                if (alertResponse?.Value != null)
                {
                    _logger.LogInformation($"Found {alertResponse.Value.Count} alerts from REST API");

                    foreach (var alertItem in alertResponse.Value)
                    {
                        var essentials = alertItem.Properties.Essentials;

                        if (DateTimeOffset.TryParse(essentials.StartDateTime, out var startTime))
                        {
                            if (essentials.MonitorCondition == "Fired" && startTime >= cutoffTime)
                            {
                                _logger.LogInformation($"Adding alert {alertItem.Id} to new alerts list - Rule: {essentials.AlertRule}, Time: {startTime}");

                                newAlerts.Add(alertItem);
                            }
                            else
                            {
                                _logger.LogDebug($"Skipping alert {alertItem.Id} - Condition: {essentials.MonitorCondition}, StartTime: {startTime}, Cutoff: {cutoffTime}");
                            }
                        }
                        else
                        {
                            _logger.LogWarning($"Could not parse start time for alert {alertItem.Id}: {essentials.StartDateTime}");
                        }
                    }
                }
            }
            else
            {
                _logger.LogError($"API call failed with status: {response.StatusCode}");
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Error response: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exception thrown when polling for AzMonitor Alerts: {ex.Message}");
            _logger.LogDebug($"Stack trace: {ex.StackTrace}");
        }

        return newAlerts;
    }

    public async Task<bool> AcknowledgeAlert(string alertId)
    {
        _logger.LogInformation($"Acknowledging alert {alertId}");
        return await UpdateAlertStatus(alertId, ServiceAlertState.Acknowledged);
    }

    public async Task<bool> UpdateAlertStatus(string alertId, ServiceAlertState alertState)
    {
        try
        {
            var credential = _authService.GetArmReadOperationCredential();
            var token = await credential.GetTokenAsync(
                new TokenRequestContext(new[] { "https://management.azure.com/.default" }),
                CancellationToken.None);

            // only support with api-version=2019-05-03-preview
            // https://learn.microsoft.com/en-us/rest/api/monitor/alertsmanagement/alerts/change-state?view=rest-monitor-alertsmanagement-2023-07-12-preview&viewFallbackFrom=rest-monitor-alertsmanagement-2019-05-05-preview&tabs=HTTP
            string apiUrl = $"https://management.azure.com{alertId}/changestate?api-version=2019-05-05-preview&newState={alertState}";

            var payload = new
            {
                comment = $"Alert {alertState} by Agent.Runtime"
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token.Token);
            request.Content = content;

            var response = await _httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation($"Alert {alertId} status updated to {alertState} successfully");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Failed to update alert status for {alertId}: {response.StatusCode} - {errorContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to update alert status for {alertId}: {ex.Message}");
            return false;
        }
    }
}

#region Classes to deserialize the REST API response
public class AlertsResponse
{
    [JsonPropertyName("value")]
    public List<AlertItem> Value { get; set; }
}

public class AlertItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; }

    [JsonPropertyName("type")]
    public string Type { get; set; }

    [JsonPropertyName("properties")]
    public AlertProperties Properties { get; set; }
}

public class AlertProperties
{
    [JsonPropertyName("essentials")]
    public AlertEssentials Essentials { get; set; }
}

public class AlertEssentials
{
    [JsonPropertyName("actionStatus")]
    public ActionStatus ActionStatus { get; set; }

    [JsonPropertyName("alertRule")]
    public string AlertRule { get; set; }

    [JsonPropertyName("alertState")]
    public string AlertState { get; set; }

    [JsonPropertyName("description")]
    public string Description { get; set; }

    [JsonPropertyName("lastModifiedDateTime")]
    public string LastModifiedDateTime { get; set; }

    [JsonPropertyName("lastModifiedUserName")]
    public string LastModifiedUserName { get; set; }

    [JsonPropertyName("monitorCondition")]
    public string MonitorCondition { get; set; }

    [JsonPropertyName("monitorService")]
    public string MonitorService { get; set; }

    [JsonPropertyName("severity")]
    public string Severity { get; set; }

    [JsonPropertyName("signalType")]
    public string SignalType { get; set; }

    [JsonPropertyName("sourceCreatedId")]
    public string SourceCreatedId { get; set; }

    [JsonPropertyName("startDateTime")]
    public string StartDateTime { get; set; }

    [JsonPropertyName("targetResource")]
    public string TargetResource { get; set; }

    [JsonPropertyName("targetResourceGroup")]
    public string TargetResourceGroup { get; set; }

    [JsonPropertyName("targetResourceName")]
    public string TargetResourceName { get; set; }

    [JsonPropertyName("targetResourceType")]
    public string TargetResourceType { get; set; }
}

public class ActionStatus
{
    [JsonPropertyName("isSuppressed")]
    public bool IsSuppressed { get; set; }
}

#endregion
