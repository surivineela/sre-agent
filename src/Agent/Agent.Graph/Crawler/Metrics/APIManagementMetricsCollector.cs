using Agent.Core.Models;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Microsoft.Extensions.Logging;

namespace Agent.Graph.Crawler.Metrics
{
    public class APIManagementMetricsCollector : IResourceMetricsCollector
    {
        private readonly ILogger<APIManagementMetricsCollector> _logger;
        private readonly IAzureMetricsClient _azureMetricsClient;
        public string ResourceType => Constants.ApiManagementType.ToLower();

        public APIManagementMetricsCollector(ILogger<APIManagementMetricsCollector> logger, IAzureMetricsClient azureMetricsClient)
        {
            _logger = logger;
            _azureMetricsClient = azureMetricsClient;
        }

        public async Task<AppHealthInfo> CollectMetricsAsync(GraphNode gnode)
        {
            if (gnode is not ArmResourceNode node)
            {
                _logger.LogInternalWarning($"Node {gnode.GetNodeId()} is not an ArmResourceNode");
                return new AppHealthInfo();
            }

            var resourceId = node.GetNodeId();

            if (resourceId == null)
            {
                _logger.LogInternalWarning($"Resource id for node {node.GetNodeLabel()} cannot be null or empty");
                return new AppHealthInfo();
            }

            try
            {
                var totalRequests = await GetTotalRequestCountAsync(resourceId);
                var avgCpuUsage = await GetAvgCpuUsagePercentAsync(resourceId);
                var avgMemUsage = await GetAvgMemoryUsageAsync(resourceId);
                var cost = await _azureMetricsClient.GetCostAsync(resourceId, DateTime.UtcNow);
                var availability = await GetAvailabilityAsync(resourceId);

                var appHealthInfo = new AppHealthInfo
                {
                    Transactions = (int)Math.Round(totalRequests),
                    AvgMemoryUsage = Math.Round(avgMemUsage, Constants.AppHealthDecimalPlaces),
                    AvgCpuUsage = Math.Round(avgCpuUsage, Constants.AppHealthDecimalPlaces),
                    Costs = Math.Round(cost, Constants.AppHealthDecimalPlaces),
                    Availability = Math.Round(availability, Constants.AppHealthDecimalPlaces),
                    Health = availability >= Constants.AppHealthHealthyThreshold ? ScorecardHealthState.Healthy :
                             availability >= Constants.AppHealthDegradedThreshold ? ScorecardHealthState.Degraded :
                             ScorecardHealthState.Unhealthy,
                };

                return appHealthInfo;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Failed to get metrics for the node {node.GetNodeId()}");
                return new AppHealthInfo();
            }
        }

        public async Task<double> GetAvgCpuUsagePercentAsync(string resourceId)
        {
            _logger.LogInternalInformation($"Getting average CPU usage for resourceId: {resourceId}");
            try
            {
                var metrics = new List<Metric>
                    {
                        new() { Name = Constants.GatewayCpuPercent, Unit = Constants.UnitPercent, Aggregation = Constants.AggregationAverage },
                    };

                var metricsData = await _azureMetricsClient.GetMetricsAsync(
                    resourceId,
                    metrics);

                return metricsData.Any() ? metricsData.Select(s => s.Value).Average() : 0;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, $"Failed to get CPU metrics for API Management Instance: {resourceId}. Will return 0.");
                return 0;
            }
        }

        public async Task<double> GetAvgMemoryUsageAsync(string resourceId)
        {
            _logger.LogInternalInformation($"Getting average Memory usage for resourceId: {resourceId}");
            try
            {
                var metrics = new List<Metric>
                    {
                        new() { Name = Constants.GatewayMemoryPercent, Unit = Constants.UnitPercent, Aggregation = Constants.AggregationAverage },
                    };

                var metricsData = await _azureMetricsClient.GetMetricsAsync(
                    resourceId,
                    metrics);

                return metricsData.Any() ? metricsData.Select(s => s.Value).Average() : 0;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, $"Failed to get memory metrics for API Management Instance: {resourceId}. Will return 0.");
                return 0;
            }
        }

        public async Task<double> GetTotalRequestCountAsync(string resourceId)
        {
            _logger.LogInternalInformation($"Getting total request count for resourceId: {resourceId}");
            try
            {
                var metrics = new List<Metric>
                    {
                        new() { Name = Constants.Requests, Unit = Constants.UnitCount, Aggregation = Constants.AggregationTotal },
                    };

                var metricsData = await _azureMetricsClient.GetMetricsAsync(
                    resourceId.ToString(),
                    metrics);

                return metricsData.Any() ? metricsData.Select(s => s.Value).Sum() : 0;
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, $"Failed to get request count metrics for API Management Instance: {resourceId}. Will return 0.");
                return 0;
            }
        }

        private async Task<double> GetAvailabilityAsync(string resourceId)
        {
            _logger.LogInternalInformation($"Getting availability for API Management Instance: {resourceId}");

            try
            {
                var metrics = new List<Metric>
                    {
                        new() { Name = Constants.Requests, Unit = Constants.UnitCount, Aggregation = Constants.AggregationTotal },
                    };

                var totalRequests = await _azureMetricsClient.GetMetricsAsync(
                    resourceId,
                    metrics);

                var errorRequests = await _azureMetricsClient.GetMetricsAsync(
                    resourceId,
                    metrics,
                    filter: "$filter=(GatewayResponseCode ne '2xx')");

                double totalRequestCount = totalRequests.Sum(s => s.Value);
                double errorRequestCount = errorRequests.Sum(s => s.Value);

                // Ensure error count doesn't exceed total count
                errorRequestCount = Math.Min(errorRequestCount, totalRequestCount);

                if (totalRequestCount == 0)
                {
                    _logger.LogInternalInformation($"[GetAvailabilityAsync] No requests found for API Management Instance: {resourceId}. Returning 100% availability by default.");
                    return 100; // No requests = 100% availability by default
                }

                return Math.Max(0, ((totalRequestCount - errorRequestCount) / totalRequestCount) * 100);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, $"Failed to calculate availability for API Management Instance: {resourceId}. Will return 0.");
                return 0;
            }
        }
    }
}
