// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Core.Models.Charts;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Metrics;
public class AzureMetricsClient : IAzureMetricsClient
{
    private readonly ILogger<AzureMetricsClient> _logger;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ArmHelper _armHelper;

    public AzureMetricsClient(ArmHelper armHelper, IHttpClientFactory httpClientFactory, ILogger<AzureMetricsClient> logger)
    {
        _armHelper = armHelper;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    /// <summary>
    /// Get cost associated with a resource.
    /// Currently, only gets weekly cost. 
    /// </summary>
    /// <param name="resourceId">Azure resource ID</param>
    /// <param name="endTime">End date for cost calculation</param>
    /// <returns>Weekly cost for the resource usage.</returns>
    public async Task<double> GetCostAsync(string resourceId, DateTime endTime)
    {
        try
        {
            _logger.LogInformation($"Getting weekly cost for resourceId: {resourceId} ending on {endTime}");

            var resourceIdentifier = new ResourceIdentifier(resourceId);
            string subscriptionId = resourceIdentifier.SubscriptionId;

            // Time range (last 7 days) 
            DateTime startTime = endTime.AddDays(-7);

            var url = new Uri(new Uri("https://management.azure.com"),
                $"/subscriptions/{subscriptionId}/providers/Microsoft.CostManagement/query?api-version=2023-11-01");

            var requestBody = new
            {
                type = "ActualCost",
                timeframe = "Custom",
                timePeriod = new
                {
                    from = startTime.ToString("yyyy-MM-ddT00:00:00"),
                    to = endTime.ToString("yyyy-MM-ddT23:59:59")
                },
                dataset = new
                {
                    granularity = "Daily",
                    aggregation = new
                    {
                        totalCost = new
                        {
                            name = "Cost",
                            function = "Sum"
                        }
                    },
                    filter = new
                    {
                        dimensions = new
                        {
                            name = "ResourceId",
                            @operator = "In",
                            values = new[] { resourceId }
                        }
                    }
                }
            };

            string jsonBody = JsonSerializer.Serialize(requestBody);

            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(jsonBody, System.Text.Encoding.UTF8, "application/json")
            };

            var httpClient = _httpClientFactory.CreateClient(nameof(ArmHelper));
            HttpResponseMessage response = await httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Error fetching cost data: {response.StatusCode}, Error: {errorContent}");
                return 0;
            }

            var content = await response.Content.ReadAsStringAsync();
            var costJson = JsonDocument.Parse(content);

            double totalCost = 0;

            var rows = costJson.RootElement.GetProperty("properties").GetProperty("rows");
            foreach (var row in rows.EnumerateArray())
            {
                totalCost += row[0].GetDouble();
            }

            _logger.LogInformation($"Total weekly cost for {resourceId}: ${totalCost}");
            return totalCost;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to fetch cost data for resource {resourceId}");
            return 0;
        }
    }

    public async Task<List<TimeSeriesData>> GetMetricsAsync(string resourceId, List<Metric> metrics, string filter = "")
    {
        // TODO: Use ArmHelper implementation for now. Will replace this with more robust once all the pieces are wired up.
        var metricsData = await _armHelper.FetchMetricsAsync(
            resourceId.ToString(),
            metrics,
            filter);

        return metricsData;
    }
}
