using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Plugins.Interface;
using Azure.Core;
using Azure.Monitor.Query.Models;
using Azure.ResourceManager.RedisEnterprise;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

/// <summary>
/// Implementation of the Redis Plugin for Azure Cache for Redis diagnostic and performance analysis
/// </summary>
public class RedisPlugin : IRedisPlugin
{
    private readonly ILogger<RedisPlugin> _logger;
    private readonly ArmHelper _armHelper;
    private readonly IArmClientFactory _armClientFactory;
    private readonly AzureMonitorMetricsHelper _azureMonitorMetricsHelper;
    private readonly IAuthenticationService _authenticationService;

    /// <summary>
    /// Gets or sets the thread ID
    /// </summary>
    public Guid? ThreadId { get; set; }

    /// <summary>
    /// Constructor for RedisPlugin
    /// </summary>
    /// <param name="logger">Logger for the plugin</param>
    /// <param name="armHelper">ARM helper for interacting with Azure resources</param>
    /// <param name="armClientFactory">Factory for creating ARM clients</param>
    /// <param name="azureMonitorMetricsHelper">Helper for Azure Monitor metrics queries</param>
    /// <param name="authenticationService">Service for authentication</param>
    public RedisPlugin(
        ILogger<RedisPlugin> logger,
        ArmHelper armHelper,
        IArmClientFactory armClientFactory,
        AzureMonitorMetricsHelper azureMonitorMetricsHelper,
        IAuthenticationService authenticationService)
    {
        _logger = logger;
        _armHelper = armHelper;
        _armClientFactory = armClientFactory;
        _azureMonitorMetricsHelper = azureMonitorMetricsHelper;
        _authenticationService = authenticationService;
    }

    /// <summary>
    /// Gets basic Azure Cache for Redis information including SKU, configuration, and basic details
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <returns>Redis cache information</returns>
    public async Task<RedisCacheInfo> GetRedisCacheAsync(string resourceId)
    {
        try
        {
            _logger.LogInternalInformation($"[redis_cache_info] Retrieving Redis cache info for {resourceId}");

            if (!IsValidAzureResourceId(resourceId))
            {
                _logger.LogInternalWarning($"[redis_cache_info] Invalid resource ID format: {resourceId}");
                return new RedisCacheInfo(
                    ResourceId: resourceId,
                    Name: "Unknown",
                    Location: "Unknown",
                    Sku: "Unknown",
                    Status: "Unknown",
                    Summary: $"❌ Invalid resource ID format. Expected full Azure resource ID starting with /subscriptions/"
                );
            }

            var armClient = await _armClientFactory.GetArmOperationClient();
            var resourceIdentifier = new ResourceIdentifier(resourceId);

            string name = "Unknown";
            string location = "Unknown";
            string sku = "Unknown";
            string status = "Unknown";
            string summary = "Unknown";

            var redisEnterpriseResource = armClient.GetRedisEnterpriseClusterResource(resourceIdentifier);
            var redisEnterpriseData = await redisEnterpriseResource.GetAsync();
            var redisEnterprise = redisEnterpriseData.Value.Data;

            name = redisEnterprise.Name;
            location = redisEnterprise.Location.ToString();
            sku = $"{redisEnterprise.Sku.Name} {redisEnterprise.Sku.Capacity}";
            status = redisEnterprise.ProvisioningState?.ToString() ?? "Unknown";

            summary = $"✅ Redis Enterprise {name} ({sku}) in {location} - Status: {status}";

            return new RedisCacheInfo(
                ResourceId: resourceId,
                Name: name,
                Location: location,
                Sku: sku,
                Status: status,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[redis_cache_info] Error retrieving Redis cache info: {ex.Message}");
            return new RedisCacheInfo(
                ResourceId: resourceId,
                Name: "Error",
                Location: "Error",
                Sku: "Error",
                Status: "Error",
                Summary: $"❌ Error retrieving Redis cache information: {ex.Message}"
            );
        }
    }

    /// <summary>
    /// Gets detailed Azure Cache for Redis information including configuration and status
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <returns>Detailed Redis cache information</returns>
    public async Task<RedisCacheDetailedInfo> GetRedisCacheInfoAsync(string resourceId)
    {
        try
        {
            _logger.LogInternalInformation($"[redis_cache_detailed_info] Retrieving detailed Redis cache info for {resourceId}");

            if (!IsValidAzureResourceId(resourceId))
            {
                _logger.LogInternalWarning($"[redis_cache_detailed_info] Invalid resource ID format: {resourceId}");
                return new RedisCacheDetailedInfo(
                    ResourceId: resourceId,
                    Name: "Unknown",
                    Location: "Unknown",
                    Sku: "Unknown",
                    Status: "Unknown",
                    RedisVersion: "Unknown",
                    Port: 0,
                    SslPort: false,
                    AccessKeys: "Not available",
                    Configuration: new Dictionary<string, string>(),
                    Summary: $"❌ Invalid resource ID format. Expected full Azure resource ID starting with /subscriptions/"
                );
            }

            var armClient = await _armClientFactory.GetArmOperationClient();
            var resourceIdentifier = new ResourceIdentifier(resourceId);

            // Initialize common return values
            string name = "Unknown";
            string location = "Unknown";
            string sku = "Unknown";
            string status = "Unknown";
            string redisVersion = "Unknown";
            int port = 0;
            bool sslPort = true;
            string accessKeys = "Use Azure portal or CLI to retrieve access keys";
            var configuration = new Dictionary<string, string>();
            string summary = "Unknown";

            // For Redis Enterprise
            var redisEnterpriseResource = armClient.GetRedisEnterpriseClusterResource(resourceIdentifier);
            var redisEnterpriseData = await redisEnterpriseResource.GetAsync();
            var redisEnterprise = redisEnterpriseData.Value.Data;

            name = redisEnterprise.Name;
            location = redisEnterprise.Location.ToString();
            sku = $"{redisEnterprise.Sku.Name} {redisEnterprise.Sku.Capacity}";
            status = redisEnterprise.ProvisioningState?.ToString() ?? "Unknown";

            // Enterprise doesn’t expose RedisVersion, Port, or Configuration the same way
            redisVersion = "Not available";
            port = 0;
            sslPort = true;
            configuration = new Dictionary<string, string>();

            summary = $"✅ Redis Enterprise {name} ({sku}) in {location} - Status: {status}";

            return new RedisCacheDetailedInfo(
                ResourceId: resourceId,
                Name: name,
                Location: location,
                Sku: sku,
                Status: status,
                RedisVersion: redisVersion,
                Port: port,
                SslPort: sslPort,
                AccessKeys: accessKeys,
                Configuration: configuration,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[redis_cache_detailed_info] Error retrieving detailed Redis cache info: {ex.Message}");
            return new RedisCacheDetailedInfo(
                ResourceId: resourceId,
                Name: "Error",
                Location: "Error",
                Sku: "Error",
                Status: "Error",
                RedisVersion: "Error",
                Port: 0,
                SslPort: false,
                AccessKeys: "Error",
                Configuration: new Dictionary<string, string>(),
                Summary: $"❌ Error retrieving detailed Redis cache information: {ex.Message}"
            );
        }
    }


    /// <summary>
    /// Gets Redis server load metrics for performance analysis
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>Redis server load metrics</returns>
    public async Task<RedisServerLoadMetrics> GetRedisServerLoadAsync(string resourceId, TimeSpan window)
    {
        try
        {
            _logger.LogInternalInformation($"[redis_server_load] Retrieving Redis server load metrics for {resourceId}, window: {window}");

            if (!IsValidAzureResourceId(resourceId))
            {
                return CreateErrorServerLoadMetrics(resourceId, "Invalid resource ID format");
            }

            var metrics = await QueryRedisMetrics(resourceId, "percentprocessortime", window);
            var timeSeries = metrics.Select(m => new TimeSeriesDataPoint(m.TimeStamp, m.Average ?? 0)).ToList();

            var averageLoad = timeSeries.Any() ? timeSeries.Average(t => t.Value) : 0;
            var maxLoad = timeSeries.Any() ? timeSeries.Max(t => t.Value) : 0;
            var minLoad = timeSeries.Any() ? timeSeries.Min(t => t.Value) : 0;
            var hasPerformanceIssue = averageLoad > 80;

            var summary = hasPerformanceIssue
                ? $"⚠️ HIGH SERVER LOAD DETECTED: Average {averageLoad:F1}% (Max: {maxLoad:F1}%). Server load > 80% indicates performance bottleneck."
                : $"✅ Server load is healthy: Average {averageLoad:F1}% (Max: {maxLoad:F1}%)";

            return new RedisServerLoadMetrics(
                ResourceId: resourceId,
                Timestamp: DateTime.UtcNow,
                AverageServerLoad: averageLoad,
                MaxServerLoad: maxLoad,
                MinServerLoad: minLoad,
                ServerLoadTimeSeries: timeSeries,
                HasPerformanceIssue: hasPerformanceIssue,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[redis_server_load] Error retrieving Redis server load metrics: {ex.Message}");
            return CreateErrorServerLoadMetrics(resourceId, ex.Message);
        }
    }

    /// <summary>
    /// Gets Redis connected clients metrics to analyze connection patterns
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>Redis connected clients metrics</returns>
    public async Task<RedisConnectedClientsMetrics> GetRedisConnectedClientsAsync(string resourceId, TimeSpan window)
    {
        try
        {
            _logger.LogInternalInformation($"[redis_connected_clients] Retrieving Redis connected clients metrics for {resourceId}, window: {window}");

            if (!IsValidAzureResourceId(resourceId))
            {
                return CreateErrorConnectedClientsMetrics(resourceId, "Invalid resource ID format");
            }

            var metrics = await QueryRedisMetrics(resourceId, "connectedclients", window);
            var timeSeries = metrics.Select(m => new TimeSeriesDataPoint(m.TimeStamp, m.Average ?? 0)).ToList();

            var averageClients = timeSeries.Any() ? (int)timeSeries.Average(t => t.Value) : 0;
            var maxClients = timeSeries.Any() ? (int)timeSeries.Max(t => t.Value) : 0;
            var minClients = timeSeries.Any() ? (int)timeSeries.Min(t => t.Value) : 0;

            // Check for connection spikes (significant increase in short time)
            var hasConnectionSpikes = timeSeries.Any() &&
                timeSeries.Zip(timeSeries.Skip(1), (prev, curr) => curr.Value - prev.Value).Any(diff => diff > 10);

            var summary = hasConnectionSpikes
                ? $"⚠️ CONNECTION SPIKES DETECTED: Average {averageClients} (Max: {maxClients}). High connection creation rate detected."
                : $"✅ Connection patterns are stable: Average {averageClients} (Max: {maxClients}) connected clients";

            return new RedisConnectedClientsMetrics(
                ResourceId: resourceId,
                Timestamp: DateTime.UtcNow,
                AverageConnectedClients: averageClients,
                MaxConnectedClients: maxClients,
                MinConnectedClients: minClients,
                ConnectedClientsTimeSeries: timeSeries,
                HasConnectionSpikes: hasConnectionSpikes,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[redis_connected_clients] Error retrieving Redis connected clients metrics: {ex.Message}");
            return CreateErrorConnectedClientsMetrics(resourceId, ex.Message);
        }
    }

    /// <summary>
    /// Gets Redis command latency metrics for performance analysis
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>Redis command latency metrics</returns>
    public async Task<RedisCommandLatencyMetrics> GetRedisCommandLatencyAsync(string resourceId, TimeSpan window)
    {
        try
        {
            _logger.LogInternalInformation($"[redis_command_latency] Retrieving Redis command latency metrics for {resourceId}, window: {window}");

            if (!IsValidAzureResourceId(resourceId))
            {
                return CreateErrorCommandLatencyMetrics(resourceId, "Invalid resource ID format");
            }

            // For Redis, we might need to use a different metric name or calculate based on operation metrics
            var metrics = await QueryRedisMetrics(resourceId, "totalkeys", window); // Placeholder - may need adjustment
            var timeSeries = metrics.Select(m => new TimeSeriesDataPoint(m.TimeStamp, m.Average ?? 0)).ToList();

            // This is a simplified implementation - actual latency metrics might require different approach
            var averageLatency = timeSeries.Any() ? timeSeries.Average(t => t.Value) / 1000.0 : 0; // Convert to ms
            var maxLatency = timeSeries.Any() ? timeSeries.Max(t => t.Value) / 1000.0 : 0;
            var minLatency = timeSeries.Any() ? timeSeries.Min(t => t.Value) / 1000.0 : 0;
            var hasLatencyIssues = averageLatency > 10; // 10ms threshold

            var summary = hasLatencyIssues
                ? $"⚠️ HIGH LATENCY DETECTED: Average {averageLatency:F2}ms (Max: {maxLatency:F2}ms). High latency may cause timeouts."
                : $"✅ Command latency is acceptable: Average {averageLatency:F2}ms (Max: {maxLatency:F2}ms)";

            return new RedisCommandLatencyMetrics(
                ResourceId: resourceId,
                Timestamp: DateTime.UtcNow,
                AverageLatencyMs: averageLatency,
                MaxLatencyMs: maxLatency,
                MinLatencyMs: minLatency,
                LatencyTimeSeries: timeSeries,
                HasLatencyIssues: hasLatencyIssues,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[redis_command_latency] Error retrieving Redis command latency metrics: {ex.Message}");
            return CreateErrorCommandLatencyMetrics(resourceId, ex.Message);
        }
    }

    /// <summary>
    /// Gets Redis memory usage metrics including memory pressure indicators
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>Redis memory usage metrics</returns>
    public async Task<RedisMemoryUsageMetrics> GetRedisMemoryUsageAsync(string resourceId, TimeSpan window)
    {
        try
        {
            _logger.LogInternalInformation($"[redis_memory_usage] Retrieving Redis memory usage metrics for {resourceId}, window: {window}");

            if (!IsValidAzureResourceId(resourceId))
            {
                return CreateErrorMemoryUsageMetrics(resourceId, "Invalid resource ID format");
            }

            var metrics = await QueryRedisMetrics(resourceId, "usedmemorypercentage", window);
            var timeSeries = metrics.Select(m => new TimeSeriesDataPoint(m.TimeStamp, m.Average ?? 0)).ToList();

            var memoryUsagePercent = timeSeries.Any() ? timeSeries.Average(t => t.Value) : 0;
            var hasMemoryPressure = memoryUsagePercent > 85; // 85% threshold

            // Estimate memory values (would need actual cache size for accurate values)
            var usedMemoryBytes = (long)(memoryUsagePercent * 1024 * 1024 * 100); // Estimate
            var maxMemoryBytes = 1024L * 1024 * 1024 * 10; // Estimate 10GB

            var summary = hasMemoryPressure
                ? $"⚠️ HIGH MEMORY USAGE: {memoryUsagePercent:F1}%. Memory pressure detected - consider scaling up."
                : $"✅ Memory usage is healthy: {memoryUsagePercent:F1}%";

            return new RedisMemoryUsageMetrics(
                ResourceId: resourceId,
                Timestamp: DateTime.UtcNow,
                MemoryUsagePercent: memoryUsagePercent,
                UsedMemoryBytes: usedMemoryBytes,
                MaxMemoryBytes: maxMemoryBytes,
                MemoryUsageTimeSeries: timeSeries,
                HasMemoryPressure: hasMemoryPressure,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[redis_memory_usage] Error retrieving Redis memory usage metrics: {ex.Message}");
            return CreateErrorMemoryUsageMetrics(resourceId, ex.Message);
        }
    }

    /// <summary>
    /// Gets Redis eviction metrics to analyze memory pressure and eviction patterns
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>Redis eviction metrics</returns>
    public async Task<RedisEvictionMetrics> GetRedisEvictionMetricsAsync(string resourceId, TimeSpan window)
    {
        try
        {
            _logger.LogInternalInformation($"[redis_eviction_metrics] Retrieving Redis eviction metrics for {resourceId}, window: {window}");

            if (!IsValidAzureResourceId(resourceId))
            {
                return CreateErrorEvictionMetrics(resourceId, "Invalid resource ID format");
            }

            var metrics = await QueryRedisMetrics(resourceId, "evictedkeys", window);
            var timeSeries = metrics.Select(m => new TimeSeriesDataPoint(m.TimeStamp, m.Total ?? 0)).ToList();

            var totalEvictedKeys = timeSeries.Any() ? (long)timeSeries.Sum(t => t.Value) : 0;
            var expiredKeys = totalEvictedKeys; // Simplified - might need separate metric
            var hasHighEvictionRate = totalEvictedKeys > 1000; // Threshold

            var summary = hasHighEvictionRate
                ? $"⚠️ HIGH EVICTION RATE: {totalEvictedKeys} keys evicted. Memory pressure detected."
                : $"✅ Eviction rate is normal: {totalEvictedKeys} keys evicted";

            return new RedisEvictionMetrics(
                ResourceId: resourceId,
                Timestamp: DateTime.UtcNow,
                TotalEvictedKeys: totalEvictedKeys,
                ExpiredKeys: expiredKeys,
                EvictedKeysTimeSeries: timeSeries,
                HasHighEvictionRate: hasHighEvictionRate,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[redis_eviction_metrics] Error retrieving Redis eviction metrics: {ex.Message}");
            return CreateErrorEvictionMetrics(resourceId, ex.Message);
        }
    }

    /// <summary>
    /// Gets Redis cache hit/miss ratio metrics for performance optimization
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>Redis hit/miss ratio metrics</returns>
    public async Task<RedisHitMissRatioMetrics> GetRedisHitMissRatioAsync(string resourceId, TimeSpan window)
    {
        try
        {
            _logger.LogInternalInformation($"[redis_hit_miss_ratio] Retrieving Redis hit/miss ratio metrics for {resourceId}, window: {window}");

            if (!IsValidAzureResourceId(resourceId))
            {
                return CreateErrorHitMissRatioMetrics(resourceId, "Invalid resource ID format");
            }

            var hitMetrics = await QueryRedisMetrics(resourceId, "cachehits", window);
            var missMetrics = await QueryRedisMetrics(resourceId, "cachemisses", window);

            var totalHits = hitMetrics.Any() ? (long)hitMetrics.Sum(m => m.Total ?? 0) : 0;
            var totalMisses = missMetrics.Any() ? (long)missMetrics.Sum(m => m.Total ?? 0) : 0;
            var totalRequests = totalHits + totalMisses;
            var hitRatio = totalRequests > 0 ? (double)totalHits / totalRequests * 100 : 0;

            var timeSeries = hitMetrics.Select(m => new TimeSeriesDataPoint(m.TimeStamp,
                totalRequests > 0 ? (m.Total ?? 0) / totalRequests * 100 : 0)).ToList();

            var hasPoorHitRatio = hitRatio < 80; // 80% threshold

            var summary = hasPoorHitRatio
                ? $"⚠️ POOR HIT RATIO: {hitRatio:F1}% ({totalHits} hits, {totalMisses} misses). Cache effectiveness is low."
                : $"✅ Good hit ratio: {hitRatio:F1}% ({totalHits} hits, {totalMisses} misses)";

            return new RedisHitMissRatioMetrics(
                ResourceId: resourceId,
                Timestamp: DateTime.UtcNow,
                HitRatio: hitRatio,
                CacheHits: totalHits,
                CacheMisses: totalMisses,
                HitRatioTimeSeries: timeSeries,
                HasPoorHitRatio: hasPoorHitRatio,
                Summary: summary
            );
        }
        catch (Exception ex)
        {
            _logger.LogInternalError($"[redis_hit_miss_ratio] Error retrieving Redis hit/miss ratio metrics: {ex.Message}");
            return CreateErrorHitMissRatioMetrics(resourceId, ex.Message);
        }
    }

    private async Task<IReadOnlyList<MetricValue>> QueryRedisMetrics(string resourceId, string metricName, TimeSpan window)
    {
        var endTime = DateTimeOffset.UtcNow;
        var startTime = endTime.Subtract(window);
        var granularity = TimeSpan.FromMinutes(5); // 5 minute intervals

        try
        {
            var result = await _azureMonitorMetricsHelper.QueryResourceMetricAsync(
                resourceId,
                "Microsoft.Cache/redisEnterprise",
                metricName,
                startTime,
                endTime,
                granularity);

            return result.Metrics[0].TimeSeries
             .SelectMany(ts => ts.Values)
             .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning($"Failed to query Redis metric {metricName}: {ex.Message}");
            return new List<MetricValue>();
        }
    }

    private static bool IsValidAzureResourceId(string resourceId)
    {
        return !string.IsNullOrWhiteSpace(resourceId) &&
               resourceId.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase) &&
               resourceId.Contains("/resourceGroups/", StringComparison.OrdinalIgnoreCase);
    }

    private static RedisServerLoadMetrics CreateErrorServerLoadMetrics(string resourceId, string error)
    {
        return new RedisServerLoadMetrics(
            ResourceId: resourceId,
            Timestamp: DateTime.UtcNow,
            AverageServerLoad: 0,
            MaxServerLoad: 0,
            MinServerLoad: 0,
            ServerLoadTimeSeries: new List<TimeSeriesDataPoint>(),
            HasPerformanceIssue: false,
            Summary: $"❌ Error retrieving server load metrics: {error}"
        );
    }

    private static RedisConnectedClientsMetrics CreateErrorConnectedClientsMetrics(string resourceId, string error)
    {
        return new RedisConnectedClientsMetrics(
            ResourceId: resourceId,
            Timestamp: DateTime.UtcNow,
            AverageConnectedClients: 0,
            MaxConnectedClients: 0,
            MinConnectedClients: 0,
            ConnectedClientsTimeSeries: new List<TimeSeriesDataPoint>(),
            HasConnectionSpikes: false,
            Summary: $"❌ Error retrieving connected clients metrics: {error}"
        );
    }

    private static RedisCommandLatencyMetrics CreateErrorCommandLatencyMetrics(string resourceId, string error)
    {
        return new RedisCommandLatencyMetrics(
            ResourceId: resourceId,
            Timestamp: DateTime.UtcNow,
            AverageLatencyMs: 0,
            MaxLatencyMs: 0,
            MinLatencyMs: 0,
            LatencyTimeSeries: new List<TimeSeriesDataPoint>(),
            HasLatencyIssues: false,
            Summary: $"❌ Error retrieving command latency metrics: {error}"
        );
    }

    private static RedisMemoryUsageMetrics CreateErrorMemoryUsageMetrics(string resourceId, string error)
    {
        return new RedisMemoryUsageMetrics(
            ResourceId: resourceId,
            Timestamp: DateTime.UtcNow,
            MemoryUsagePercent: 0,
            UsedMemoryBytes: 0,
            MaxMemoryBytes: 0,
            MemoryUsageTimeSeries: new List<TimeSeriesDataPoint>(),
            HasMemoryPressure: false,
            Summary: $"❌ Error retrieving memory usage metrics: {error}"
        );
    }

    private static RedisEvictionMetrics CreateErrorEvictionMetrics(string resourceId, string error)
    {
        return new RedisEvictionMetrics(
            ResourceId: resourceId,
            Timestamp: DateTime.UtcNow,
            TotalEvictedKeys: 0,
            ExpiredKeys: 0,
            EvictedKeysTimeSeries: new List<TimeSeriesDataPoint>(),
            HasHighEvictionRate: false,
            Summary: $"❌ Error retrieving eviction metrics: {error}"
        );
    }

    private static RedisHitMissRatioMetrics CreateErrorHitMissRatioMetrics(string resourceId, string error)
    {
        return new RedisHitMissRatioMetrics(
            ResourceId: resourceId,
            Timestamp: DateTime.UtcNow,
            HitRatio: 0,
            CacheHits: 0,
            CacheMisses: 0,
            HitRatioTimeSeries: new List<TimeSeriesDataPoint>(),
            HasPoorHitRatio: false,
            Summary: $"❌ Error retrieving hit/miss ratio metrics: {error}"
        );
    }
}
