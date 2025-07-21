// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Metrics;

public class ContainerAppMetricsCollector : IResourceMetricsCollector
{
    private readonly ILogger<ContainerAppMetricsCollector> _logger;
    private readonly IAzureMetricsClient _azureMetricsClient;
    public string ResourceType => "microsoft.app/containerapps";

    public ContainerAppMetricsCollector(ILogger<ContainerAppMetricsCollector> logger, IAzureMetricsClient azureMetricsClient)
    {
        _logger = logger;
        _azureMetricsClient = azureMetricsClient;
    }

    public async Task<AppHealthInfo> CollectMetricsAsync(GraphNode gnode)
    {
        if (gnode is not ArmResourceNode node)
        {
            _logger.LogInternalWarning($"Node {gnode.GetNodeId()} is not an ArmResourceNode");
            return new AppHealthInfo { };
        }

        var resourceId = node.GetNodeId();

        if (resourceId == null)
        {
            _logger.LogInternalWarning($"Resource id for node {node.GetNodeLabel()} cannot be null or empty");
            return new AppHealthInfo { };
        }

        var now = DateTime.UtcNow;
        var startTime = now.AddMinutes(-30); // TODO: Maybe make this configurable?

        try
        {
            var avgRequests = await GetAvgRequestCountAsync(resourceId);
            var avgCpuUsage = await GetAvgCpuUsagePercentAsync(resourceId);
            var avgMemUsage = await GetAvgMemoryUsageAsync(resourceId);
            var availability = await GetAvailabilityAsync(resourceId);

            var appHealthInfo = new AppHealthInfo
            {
                Transactions = (int)Math.Round(avgRequests),
                AvgMemoryUsage = Math.Round(avgMemUsage, 2),
                AvgCpuUsage = Math.Round(avgCpuUsage, 2),
                Availability = Math.Round(availability, 2),
                Health = availability >= 99.0 ? ScorecardHealthState.Healthy :
                         availability >= 95.0 ? ScorecardHealthState.Degraded :
                         ScorecardHealthState.Unhealthy,
            };

            return appHealthInfo;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to get metrics for the node {node.GetNodeId()}");
        }

        return new AppHealthInfo { };
    }

    public async Task<double> GetAvgCpuUsagePercentAsync(string resourceId)
    {
        _logger.LogInternalInformation($"Getting average CPU usage for resourceId: {resourceId}]");
        try
        {
            var metrics = new List<Metric>
            {
                new() { Name = "CpuPercentage", Unit = "Percentage", Aggregation = "Average" },
            };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId.ToString(),
                metrics);

            return metricsData.Any() ? metricsData.Select(s => s.Value).Average() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to get CPU metrics for Container App: {resourceId}. Will return 0.");
            return 0;
        }
    }

    public async Task<double> GetAvgMemoryUsageAsync(string resourceId)
    {
        _logger.LogInternalInformation($"Getting average Memory usage for resourceId: {resourceId}]");
        try
        {
            var metrics = new List<Metric>
            {
                new() { Name = "MemoryPercentage", Unit = "Percentage", Aggregation = "Average" },
            };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId.ToString(),
                metrics);

            return metricsData.Any() ? metricsData.Select(s => s.Value).Average() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to get memory metrics for Container App: {resourceId}. Will return 0.");
            return 0;
        }
    }

    public async Task<double> GetAvgRequestCountAsync(string resourceId)
    {
        _logger.LogInternalInformation($"Getting average request count for resourceId: {resourceId}]");
        try
        {
            var metrics = new List<Metric>
            {
                new() { Name = "Requests", Unit = "Count", Aggregation = "Average" },
            };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId.ToString(),
                metrics);

            return metricsData.Any() ? metricsData.Select(s => s.Value).Average() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to get request count metrics for Container App: {resourceId}. Will return 0.");
            return 0;
        }
    }

    private async Task<double> GetAvailabilityAsync(string resourceId)
    {
        _logger.LogInternalInformation($"Getting availability for Container App: {resourceId}");

        try
        {
            var metrics = new List<Metric>
            {
                new() { Name = "Requests", Unit = "Count", Aggregation = "Total" },
            };

            var totalRequests = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics);

            var errorRequests = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics,
                filter: "$filter=(statusCodeCategory ne '2xx')");

            double totalRequestCount = totalRequests.Sum(s => s.Value);
            double errorRequestCount = errorRequests.Sum(s => s.Value);

            // Ensure error count doesn't exceed total count
            errorRequestCount = Math.Min(errorRequestCount, totalRequestCount);

            if (totalRequestCount == 0)
                return 100; // No requests = 100% availability by default

            return Math.Max(0, ((totalRequestCount - errorRequestCount) / totalRequestCount) * 100);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to calculate availability for Container App: {resourceId}. Will return 0.");
            return 0;
        }
    }
}
