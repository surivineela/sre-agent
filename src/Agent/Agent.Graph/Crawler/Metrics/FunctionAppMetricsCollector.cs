// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Metrics;

public class FunctionAppMetricsCollector : IResourceMetricsCollector
{
    private readonly ILogger<FunctionAppMetricsCollector> _logger;
    private readonly IAzureMetricsClient _azureMetricsClient;
    public string ResourceType => "microsoft.web/sites/functions"; // TODO: get the resource type from graph

    public FunctionAppMetricsCollector(ILogger<FunctionAppMetricsCollector> logger, IAzureMetricsClient azureMetricsClient)
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

        // Check if it's a function app by looking at the properties
        if (!IsFunctionApp(node))
        {
            _logger.LogInternalInformation($"Node {node.GetNodeId()} is not a function app, skipping metrics collection");
            return new AppHealthInfo { };
        }

        if (resourceId == null)
        {
            _logger.LogInternalWarning($"Resource id for node {node.GetNodeLabel()} cannot be null or empty");
            return new AppHealthInfo { };
        }

        var now = DateTime.UtcNow;
        var startTime = now.AddMinutes(-30);

        try
        {
            var functionExecutions = await GetFunctionExecutionsAsync(resourceId);
            var avgCpuUsage = await GetAvgCpuUsageAsync(resourceId);
            var avgMemUsage = await GetAvgMemoryUsageAsync(resourceId);
            var availability = await GetAvailabilityAsync(resourceId);

            var appHealthInfo = new AppHealthInfo
            {
                Transactions = (int)Math.Round(functionExecutions),
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
            _logger.LogInternalError(ex, $"Failed to get metrics for the Function App {node.GetNodeId()}");
        }

        return new AppHealthInfo { };
    }

    private bool IsFunctionApp(ArmResourceNode node)
    {
        // Function apps typically have a kind property that contains "functionapp"
        var properties = node.GetNodeProperties();
        if (properties.TryGetValue("kind", out var kind))
        {
            return kind?.ToString()?.Contains("functionapp", StringComparison.OrdinalIgnoreCase) ?? false;
        }
        return false;
    }

    private async Task<double> GetAvgCpuUsageAsync(string resourceId)
    {
        _logger.LogInternalInformation($"Getting average CPU usage for Function App: {resourceId}");
        var metrics = new List<Metric>
        {
            new() { Name = "FunctionExecutionUnits", Unit = "Count", Aggregation = "Total" },
        };

        var metricsData = await _azureMetricsClient.GetMetricsAsync(
            resourceId,
            metrics);

        return metricsData.Any() ? metricsData.Select(s => s.Value).Average() : 0;
    }

    private async Task<double> GetAvgMemoryUsageAsync(string resourceId)
    {
        _logger.LogInternalInformation($"Getting average Memory usage for Function App: {resourceId}");
        try
        {
            var metrics = new List<Metric>
            {
                new() { Name = "MemoryWorkingSet", Unit = "Bytes", Aggregation = "Average" },
            };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics);

            return metricsData.Any() ? metricsData.Select(s => s.Value).Average() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to get memory metrics for Function App: {resourceId}. Will return 0.");
            return 0;
        }
    }

    private async Task<double> GetFunctionExecutionsAsync(string resourceId)
    {
        _logger.LogInternalInformation($"Getting function executions for Function App: {resourceId}");
        try
        {
            var metrics = new List<Metric>
            {
                new() { Name = "FunctionExecutionCount", Unit = "Count", Aggregation = "Total" },
            };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics);

            return metricsData.Any() ? metricsData.Select(s => s.Value).Sum() : 0;
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to get execution metrics for Function App: {resourceId}. Will return 0.");
            return 0;
        }
    }

    private async Task<double> GetAvailabilityAsync(string resourceId)
    {
        _logger.LogInternalInformation($"Getting availability for Function App: {resourceId}");
        try
        {
            // For Function Apps, calculate availability based on HTTP status codes
            var metrics = new List<Metric>
            {
                new() { Name = "Requests", Unit = "Count", Aggregation = "Total" },
                new() { Name = "Http5xx", Unit = "Count", Aggregation = "Total" }
            };

            var metricsData = await _azureMetricsClient.GetMetricsAsync(
                resourceId,
                metrics);

            var requests = metricsData.FirstOrDefault(m => m.Name == "Requests")?.Value ?? 0;
            var errors = metricsData.FirstOrDefault(m => m.Name == "Http5xx")?.Value ?? 0;

            // Ensure error count doesn't exceed total requests count
            errors = Math.Min(errors, requests);

            if (requests == 0)
            {
                return 100; // No requests = 100% availability by default
            }

            return Math.Max(0, (requests - errors) / requests * 100);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, $"Failed to get availability metrics for Function App: {resourceId}. Will return 100% (default).");
            return 100;
        }
    }
}
