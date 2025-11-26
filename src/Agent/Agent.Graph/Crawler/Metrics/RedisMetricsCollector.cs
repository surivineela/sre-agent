// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Metrics;

public class RedisMetricsCollector : IResourceMetricsCollector
{
    private readonly ILogger<RedisMetricsCollector> _logger;
    private readonly IAzureMetricsClient _azureMetricsClient;
    public string ResourceType => "microsoft.cache/redis";

    public RedisMetricsCollector(ILogger<RedisMetricsCollector> logger, IAzureMetricsClient azureMetricsClient)
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
        var startTime = now.AddMinutes(-30);

        try
        {
            var cacheHits = await GetCacheHitsAsync(resourceId);
            var cpuUsage = await GetCpuUsageAsync(resourceId);
            var memoryUsage = await GetMemoryUsageAsync(resourceId);
            var serverLoad = await GetServerLoadAsync(resourceId);

            // Determine health based on CPU and memory metrics
            var health = DetermineHealthState(cpuUsage, memoryUsage);

            var appHealthInfo = new AppHealthInfo
            {
                Transactions = (int)Math.Round(cacheHits),
                AvgMemoryUsage = Math.Round(memoryUsage, 2),
                AvgCpuUsage = Math.Round(cpuUsage, 2),
                // Use AdditionalMetrics for Redis-specific metrics
                AdditionalMetrics = new Dictionary<string, object>
                {
                    { "ServerLoad", Math.Round(serverLoad, 2) }
                },
                Health = health
            };

            return appHealthInfo;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to get metrics for the Redis Cache {node.GetNodeId()}");
        }

        return new AppHealthInfo { };
    }

    private async Task<double> GetCpuUsageAsync(string resourceId)
    {
        _logger.LogInternalInformation($"Getting CPU usage for Redis Cache: {resourceId}");
        try
        {
            var metrics = new List<Metric>
        {
            new() { Name = "percentProcessorTime", Unit = "Percent", Aggregation = "Average" },
        };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics);

            return metricsData.Any() ? metricsData.Select(s => s.Value).Average() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to get CPU metrics for Redis Cache: {resourceId}. Will return 0.");
            return 0;
        }
    }

    private async Task<double> GetMemoryUsageAsync(string resourceId)
    {
        _logger.LogInternalInformation($"Getting memory usage for Redis Cache: {resourceId}");
        try
        {
            var metrics = new List<Metric>
        {
            new() { Name = "usedmemorypercentage", Unit = "Percent", Aggregation = "Average" },
        };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics);

            return metricsData.Any() ? metricsData.Select(s => s.Value).Average() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to get memory metrics for Redis Cache: {resourceId}. Will return 0.");
            return 0;
        }
    }

    private async Task<double> GetCacheHitsAsync(string resourceId)
    {
        _logger.LogInternalInformation($"Getting cache hits for Redis Cache: {resourceId}");
        try
        {
            var metrics = new List<Metric>
        {
            new() { Name = "totalcommandsprocessed", Unit = "Count", Aggregation = "Total" },
        };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics);

            return metricsData.Any() ? metricsData.Select(s => s.Value).Sum() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to get cache hits metrics for Redis Cache: {resourceId}. Will return 0.");
            return 0;
        }
    }

    private async Task<double> GetServerLoadAsync(string resourceId)
    {
        _logger.LogInternalInformation($"Getting server load for Redis Cache: {resourceId}");
        try
        {
            var metrics = new List<Metric>
        {
            new() { Name = "connectedclients", Unit = "Count", Aggregation = "Maximum" },
        };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics);

            return metricsData.Any() ? metricsData.Select(s => s.Value).Average() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to get server load metrics for Redis Cache: {resourceId}. Will return 0.");
            return 0;
        }
    }

    private ScorecardHealthState DetermineHealthState(double cpuUsage, double memoryUsage)
    {
        // For Redis, we consider high CPU or memory usage as warnings
        if (cpuUsage > 90 || memoryUsage > 90)
        {
            return ScorecardHealthState.Degraded;
        }
        else if (cpuUsage > 75 || memoryUsage > 75)
        {
            return ScorecardHealthState.Unhealthy;
        }
        else
        {
            return ScorecardHealthState.Healthy;
        }
    }
}
