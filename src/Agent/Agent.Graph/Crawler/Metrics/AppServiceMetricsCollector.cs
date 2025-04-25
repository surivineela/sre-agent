// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Metrics;
public class AppServiceMetricsCollector : IResourceMetricsCollector
{
    private readonly ILogger<AppServiceMetricsCollector> _logger;
    private readonly IAzureMetricsClient _azureMetricsClient;
    public string ResourceType => "microsoft.web/sites";

    public AppServiceMetricsCollector(ILogger<AppServiceMetricsCollector> logger, IAzureMetricsClient azureMetricsClient)
    {
        _logger = logger;
        _azureMetricsClient = azureMetricsClient;
    }

    public async Task<AppHealthInfo> CollectMetricsAsync(GraphNode gnode)
    {
        if (gnode is not ArmResourceNode node)
        {
            _logger.LogWarning($"Node {gnode.GetNodeId()} is not an ArmResourceNode");
            return new AppHealthInfo { };
        }
        var resourceId = node.GetNodeId();

        if (resourceId == null)
        {
            _logger.LogWarning($"Resource id for node {node.GetNodeLabel()} cannot be null or empty");
            return new AppHealthInfo { };
        }

        var now = DateTime.UtcNow;
        var startTime = now.AddMinutes(-30);

        try
        {
            var avgRequests = await GetAvgRequestCountAsync(resourceId);
            var avgCpuUsage = await GetAvgCpuUsageAsync(resourceId);
            var avgMemUsage = await GetAvgMemoryUsageAsync(resourceId);
            var availability = await GetAvailabilityAsync(resourceId);
            var cost = await _azureMetricsClient.GetCostAsync(resourceId, now);

            var appHealthInfo = new AppHealthInfo
            {
                Transactions = Math.Round(avgRequests, 2),
                AvgMemoryUsage = Math.Round(avgMemUsage, 2),
                AvgCpuUsage = Math.Round(avgCpuUsage, 2),
                Availability = Math.Round(availability, 2),
                Costs = Math.Round(cost, 2),
                Health = availability >= 99.0 ? ScorecardHealthState.Healthy :
                        availability >= 95.0 ? ScorecardHealthState.Degraded :
                        ScorecardHealthState.Unhealthy,
            };

            return appHealthInfo;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to get metrics for the App Service {node.GetNodeId()}");
        }

        return new AppHealthInfo { };
    }

    private async Task<double> GetAvgCpuUsageAsync(string resourceId)
    {
        _logger.LogInformation($"Getting average CPU usage for App Service: {resourceId}");
        try
        {
            var metrics = new List<Metric>
            {
                new Metric { Name = "CpuTime", Unit = "Seconds", Aggregation = "Total" },
            };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics);

            if (!metricsData.Any())
                return 0;

            // Convert CPU time in seconds to percentage (each minute has 60 seconds max)
            return metricsData.Select(s => (s.Value / 60) * 100).Average();
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Failed to get CPU metrics for App Service: {resourceId}. Will return 0.");
            return 0;
        }
    }

    private async Task<double> GetAvgMemoryUsageAsync(string resourceId)
    {
        _logger.LogInformation($"Getting average Memory usage for App Service: {resourceId}");
        try
        {
            var metrics = new List<Metric>
            {
                new Metric { Name = "AverageMemoryWorkingSet", Unit = "Bytes", Aggregation = "Average" },
            };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics);

            return metricsData.Any() ? metricsData.Select(s => s.Value).Average() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Failed to get memory metrics for App Service: {resourceId}. Will return 0.");
            return 0;
        }
    }

    private async Task<double> GetAvgRequestCountAsync(string resourceId)
    {
        _logger.LogInformation($"Getting average request count for App Service: {resourceId}");
        try
        {
            var metrics = new List<Metric>
            {
                new Metric { Name = "Requests", Unit = "Count", Aggregation = "Total" },
            };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics);

            return metricsData.Any() ? metricsData.Select(s => s.Value).Average() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Failed to get request metrics for App Service: {resourceId}. Will return 0.");
            return 0;
        }
    }

    private async Task<double> GetAvailabilityAsync(string resourceId)
    {
        _logger.LogInformation($"Getting availability for App Service: {resourceId}");
        try
        {
            // For App Service, we calculate availability as the percentage of successful requests
            var metrics = new List<Metric>
            {
                new Metric { Name = "Requests", Unit = "Count", Aggregation = "Total" },
                new Metric { Name = "Http5xx", Unit = "Count", Aggregation = "Total" }
            };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics);

            var requests = metricsData.FirstOrDefault(m => m.Name == "Requests")?.Value ?? 0;
            var errors = metricsData.FirstOrDefault(m => m.Name == "Http5xx")?.Value ?? 0;

            // Ensure error count doesn't exceed total requests count
            errors = Math.Min(errors, requests);

            if (requests == 0)
                return 100; // No requests = 100% availability by default

            return Math.Max(0, ((requests - errors) / requests) * 100);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, $"Failed to get availability metrics for App Service: {resourceId}. Will return 100% (default).");
            return 100;
        }
    }
}
