// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Data.DataModels;
using Agent.Data.DataModels.IncidentModel;
using Agent.Data.Interface.IncidentAPI;
using Azure.ResourceManager.AlertsManagement.Models;
using Microsoft.Extensions.Logging;
using Azure.Core;

namespace Agent.Runtime.Services;

public class AzMonitorAlertService : IAzMonitorAlertService
{
    private readonly ILogger<AzMonitorAlertService> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CrawlerSettings _crawlerSettings;

    public AzMonitorAlertService(
        ILogger<AzMonitorAlertService> logger,
        CrawlerSettings crawlerSettings,
        IHttpClientFactory httpClientFactory)
    {
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _crawlerSettings = crawlerSettings;
    }

    public async Task<bool> AcknowledgeAlert(string alertId)
    {
        _logger.LogInternalInformation($"Acknowledging alert {alertId}");
        return await UpdateAlertStatus(alertId, ServiceAlertState.Acknowledged);
    }

    public async Task<bool> ResolveAlert(string alertId)
    {
        _logger.LogInternalInformation($"Resolving alert {alertId}");
        return await UpdateAlertStatus(alertId, ServiceAlertState.Closed);
    }

    public async Task<bool> UpdateAlertStatus(string alertId, ServiceAlertState alertState)
    {
        try
        {
            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

            // only supported with api-version=2019-05-03-preview
            // https://learn.microsoft.com/en-us/rest/api/monitor/alertsmanagement/alerts/change-state?view=rest-monitor-alertsmanagement-2023-07-12-preview&viewFallbackFrom=rest-monitor-alertsmanagement-2019-05-05-preview&tabs=HTTP
            string apiUrl = $"https://management.azure.com{alertId}/changestate?api-version=2019-05-05-preview&newState={alertState}";

            var payload = new
            {
                comment = $"Alert {alertState} by Agent.Runtime"
            };

            var jsonContent = JsonSerializer.Serialize(payload);
            var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Post, apiUrl);
            request.Content = content;

            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInternalInformation($"Alert {alertId} status updated to {alertState} successfully");
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogInternalError($"Failed to update alert status for {alertId}: {response.StatusCode} - {errorContent}");
                return false;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to update alert status for {alertId}: {ex.Message}");
            return false;
        }
    }

    public async Task<AlertItem> GetIncidentAsync(string alertId)
    {
        var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);
        string apiUrl = $"https://management.azure.com{alertId}?api-version=2019-05-05-preview";
        _logger.LogInternalInformation($"Calling Alert Management API with URL: {apiUrl}");

        var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);
        var response = await httpClient.SendAsync(request);
        var content = await response.Content.ReadAsStringAsync();
        if (!response.IsSuccessStatusCode)
        {
            throw new Exception($"API call failed with status: {response.StatusCode}, content: {content}");
        }

        var alertItem = JsonSerializer.Deserialize<AlertItem>(content);
        if (alertItem is null)
        {
            throw new Exception($"Failed to deserialize alert item from response content: {content}");
        }
        return alertItem;
    }

    public async Task<IEnumerable<AlertItem>> GetIncidentsAsync(
        uint limit,
        uint offset,
        DateTime? since = null,
        AzMonitorIncidentFilterDocumentPayload? filterPayload = null,
        IEnumerable<string>? statuses = null,
        Dictionary<string, string>? additionalProperties = null)
    {
        // Only support filtering by: subscriptionId, severity, title, resource groups
        if (limit == 0 || limit > 250)
        {
            throw new ArgumentOutOfRangeException(nameof(limit), "Limit must be between 1 and 250.");
        }

        var managedRGs = _crawlerSettings.CrawlRoots.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var managedRGsHashSet = new HashSet<string>(managedRGs, StringComparer.OrdinalIgnoreCase);

        string? severityParam = null; // SevX
        if (!string.IsNullOrWhiteSpace(filterPayload?.Priority))
        {
            var p = filterPayload.Priority.Trim();
            severityParam = p.StartsWith("Sev", StringComparison.OrdinalIgnoreCase) ? p : ($"Sev{p}");
        }

        var titleContains = filterPayload?.TitleContains?.Trim();

        // Polling routine already constrained to last N minutes (<=60). No direct timeRangeParam used here.
        // Collect subscriptionIds from managed RG resource IDs
        var subscriptionIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var rg in managedRGsHashSet)
        {
            try
            {
                var id = new ResourceIdentifier(rg);
                if (!string.IsNullOrWhiteSpace(id.SubscriptionId))
                {
                    subscriptionIds.Add(id.SubscriptionId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning($"Failed to parse subscription id from managed RG entry '{rg}': {ex.Message}");
            }
        }

        if (subscriptionIds.Count == 0)
        {
            _logger.LogInternalWarning("GetIncidentsAsync: No subscription ids derived from managed resource groups. Returning empty list.");
            return [];
        }

        // Calculate scan window based on since parameter
        var scanWindowInDays = since.HasValue
            ? Math.Min((int)(DateTime.UtcNow - since.Value).TotalDays + 1, 29)
            : 29;

        var aggregated = new Dictionary<string, AlertItem>(StringComparer.OrdinalIgnoreCase);

        foreach (var subId in subscriptionIds)
        {
            try
            {
                var alerts = await PollNewAlertsBySubscriptionId(subId, statuses, scanWindowInDays);
                foreach (var a in alerts)
                {
                    aggregated[a.Id] = a;
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "GetIncidentsAsync: Failed polling subscription {SubscriptionId}", subId);
            }
        }

        IEnumerable<AlertItem> filtered = aggregated.Values;

        // Severity filter (in-memory) - Essentials.Severity expected to match SevX
        if (!string.IsNullOrWhiteSpace(severityParam))
        {
            filtered = filtered.Where(a => string.Equals(a.Properties?.Essentials?.Severity, severityParam, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by Title / rule
        if (!string.IsNullOrWhiteSpace(titleContains))
        {
            filtered = filtered.Where(a =>
                (!string.IsNullOrWhiteSpace(a.Name) && a.Name.Contains(titleContains, StringComparison.OrdinalIgnoreCase)) ||
                (!string.IsNullOrWhiteSpace(a.Properties?.Essentials?.AlertRule) && a.Properties.Essentials.AlertRule.Contains(titleContains, StringComparison.OrdinalIgnoreCase))
            );
        }

        filtered = filtered.OrderByDescending(a =>
        {
            if (DateTimeOffset.TryParse(a.Properties?.Essentials?.StartDateTime, out var t)) return t;
            return DateTimeOffset.MinValue;
        });

        var finalList = filtered.Skip((int)offset).Take((int)limit).ToList();
        _logger.LogInternalInformation("GetIncidentsAsync: Returning {Count} alerts (limit={Limit}, offset={Offset}) across {SubscriptionCount} subscriptions", finalList.Count, limit, offset, subscriptionIds.Count);
        return finalList;
    }

    private async Task<IEnumerable<AlertItem>> PollNewAlertsBySubscriptionId(
        string subscriptionId,
        IEnumerable<string>? statuses = null,
        int timeWindowInDays = 1)
    {
        var newAlerts = new List<AlertItem>();

        try
        {
            _logger.LogInternalInformation($"Getting token for Azure ARM operations for subscription {subscriptionId}");
            var httpClient = _httpClientFactory.CreateClient(Constants.HttpClientForArmOperation);

            // using 2019-05-05-preview API version
            string apiUrl = $"https://management.azure.com/subscriptions/{subscriptionId}/providers/Microsoft.AlertsManagement/alerts?api-version=2019-05-05-preview";

            timeWindowInDays = Math.Min(timeWindowInDays, 29); // API supports up to < 30 days

            // End time = now (UTC)
            DateTime endTime = DateTime.UtcNow;

            // Start time = end time minus timeWindow days
            DateTime startTime = endTime.AddDays(-timeWindowInDays);

            // Format as ISO-8601 (round-trip "o" format specifier)
            string customTimeRange = $"{startTime:o}/{endTime:o}";

            apiUrl += $"&customTimeRange={customTimeRange}"; // can only do up to < 30

            // Map statuses to Azure monitor conditions and add to query
            if (statuses != null && statuses.Any())
            {
                var conditions = string.Join(",", statuses);
                apiUrl += $"&alertState={conditions}";
            }
            else
            {
                // Default to New for newly fired alerts if no statuses specified
                apiUrl += "&alertState=New";
            }

            _logger.LogInternalInformation($"Calling Alert Management API with URL: {apiUrl}");

            var request = new HttpRequestMessage(HttpMethod.Get, apiUrl);

            var response = await httpClient.SendAsync(request);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                _logger.LogDebug($"Response content: {content}");

                var alertResponse = JsonSerializer.Deserialize<AlertsResponse>(content);

                if (alertResponse?.Value != null)
                {
                    _logger.LogInternalInformation($"Found {alertResponse.Value.Count} alerts from REST API");

                    foreach (var alertItem in alertResponse.Value)
                    {
                        var essentials = alertItem.Properties.Essentials;

                        _logger.LogInternalInformation($"Adding alert {alertItem.Id} to alerts list - Rule: {essentials.AlertRule}, Time: {startTime}, State: {essentials.AlertState}, Condition: {essentials.MonitorCondition}");
                        newAlerts.Add(alertItem);
                    }
                }
            }
            else
            {
                _logger.LogInternalError($"API call failed with status: {response.StatusCode}");
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogInternalError($"Error response: {errorContent}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"Exception thrown when polling for AzMonitor Alerts: {ex.Message}");
            _logger.LogDebug($"Stack trace: {ex.StackTrace}");
        }

        return newAlerts;
    }
}

public class NullableAzMonitorAlertService : IAzMonitorAlertService
{
    public Task<bool> AcknowledgeAlert(string alertId)
    {
        return Task.FromResult(true);
    }
    public Task<bool> ResolveAlert(string alertId)
    {
        return Task.FromResult(true);
    }
    public Task<bool> UpdateAlertStatus(string alertId, ServiceAlertState alertState)
    {
        return Task.FromResult(true);
    }

    public Task<AlertItem> GetIncidentAsync(string id)
    {
        throw new NotImplementedException();
    }

    public Task<IEnumerable<AlertItem>> GetIncidentsAsync(uint limit, uint offset, DateTime? since = null, AzMonitorIncidentFilterDocumentPayload? filterPayload = null, IEnumerable<string>? status = null, Dictionary<string, string>? additionalProperties = null)
    {
        throw new NotImplementedException();
    }
}
