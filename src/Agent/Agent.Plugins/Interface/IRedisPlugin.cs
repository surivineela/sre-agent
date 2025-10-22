namespace Agent.Plugins.Interface;

/// <summary>
/// Plugin for diagnosing and analyzing Azure Cache for Redis performance and connectivity issues
/// </summary>
public interface IRedisPlugin
{
    /// <summary>
    /// Gets the thread ID for the plugin
    /// </summary>
    Guid? ThreadId { get; set; }

    /// <summary>
    /// Gets Azure Cache for Redis information including SKU, configuration, and basic details
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <returns>Redis cache information</returns>
    Task<RedisCacheInfo> GetRedisCacheAsync(string resourceId);

    /// <summary>
    /// Gets detailed Azure Cache for Redis information including configuration and status
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <returns>Detailed Redis cache information</returns>
    Task<RedisCacheDetailedInfo> GetRedisCacheInfoAsync(string resourceId);

    /// <summary>
    /// Gets Redis server load metrics for performance analysis
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>Redis server load metrics</returns>
    Task<RedisServerLoadMetrics> GetRedisServerLoadAsync(string resourceId, TimeSpan window);

    /// <summary>
    /// Gets Redis connected clients metrics to analyze connection patterns
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>Redis connected clients metrics</returns>
    Task<RedisConnectedClientsMetrics> GetRedisConnectedClientsAsync(string resourceId, TimeSpan window);

    /// <summary>
    /// Gets Redis command latency metrics for performance analysis
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>Redis command latency metrics</returns>
    Task<RedisCommandLatencyMetrics> GetRedisCommandLatencyAsync(string resourceId, TimeSpan window);

    /// <summary>
    /// Gets Redis memory usage metrics including memory pressure indicators
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>Redis memory usage metrics</returns>
    Task<RedisMemoryUsageMetrics> GetRedisMemoryUsageAsync(string resourceId, TimeSpan window);

    /// <summary>
    /// Gets Redis eviction metrics to analyze memory pressure and eviction patterns
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>Redis eviction metrics</returns>
    Task<RedisEvictionMetrics> GetRedisEvictionMetricsAsync(string resourceId, TimeSpan window);

    /// <summary>
    /// Gets Redis cache hit/miss ratio metrics for performance optimization
    /// </summary>
    /// <param name="resourceId">The full Azure resource ID of the Redis cache</param>
    /// <param name="window">Time window for metrics collection</param>
    /// <returns>Redis hit/miss ratio metrics</returns>
    Task<RedisHitMissRatioMetrics> GetRedisHitMissRatioAsync(string resourceId, TimeSpan window);
}

/// <summary>
/// Basic Redis cache information
/// </summary>
public record RedisCacheInfo(
    string ResourceId,
    string Name,
    string Location,
    string Sku,
    string Status,
    string Summary);

/// <summary>
/// Detailed Redis cache information
/// </summary>
public record RedisCacheDetailedInfo(
    string ResourceId,
    string Name,
    string Location,
    string Sku,
    string Status,
    string RedisVersion,
    int Port,
    bool SslPort,
    string AccessKeys,
    Dictionary<string, string> Configuration,
    string Summary);

/// <summary>
/// Redis server load metrics
/// </summary>
public record RedisServerLoadMetrics(
    string ResourceId,
    DateTime Timestamp,
    double AverageServerLoad,
    double MaxServerLoad,
    double MinServerLoad,
    List<TimeSeriesDataPoint> ServerLoadTimeSeries,
    bool HasPerformanceIssue,
    string Summary);

/// <summary>
/// Redis connected clients metrics
/// </summary>
public record RedisConnectedClientsMetrics(
    string ResourceId,
    DateTime Timestamp,
    int AverageConnectedClients,
    int MaxConnectedClients,
    int MinConnectedClients,
    List<TimeSeriesDataPoint> ConnectedClientsTimeSeries,
    bool HasConnectionSpikes,
    string Summary);

/// <summary>
/// Redis command latency metrics
/// </summary>
public record RedisCommandLatencyMetrics(
    string ResourceId,
    DateTime Timestamp,
    double AverageLatencyMs,
    double MaxLatencyMs,
    double MinLatencyMs,
    List<TimeSeriesDataPoint> LatencyTimeSeries,
    bool HasLatencyIssues,
    string Summary);

/// <summary>
/// Redis memory usage metrics
/// </summary>
public record RedisMemoryUsageMetrics(
    string ResourceId,
    DateTime Timestamp,
    double MemoryUsagePercent,
    long UsedMemoryBytes,
    long MaxMemoryBytes,
    List<TimeSeriesDataPoint> MemoryUsageTimeSeries,
    bool HasMemoryPressure,
    string Summary);

/// <summary>
/// Redis eviction metrics
/// </summary>
public record RedisEvictionMetrics(
    string ResourceId,
    DateTime Timestamp,
    long TotalEvictedKeys,
    long ExpiredKeys,
    List<TimeSeriesDataPoint> EvictedKeysTimeSeries,
    bool HasHighEvictionRate,
    string Summary);

/// <summary>
/// Redis hit/miss ratio metrics
/// </summary>
public record RedisHitMissRatioMetrics(
    string ResourceId,
    DateTime Timestamp,
    double HitRatio,
    long CacheHits,
    long CacheMisses,
    List<TimeSeriesDataPoint> HitRatioTimeSeries,
    bool HasPoorHitRatio,
    string Summary);

/// <summary>
/// Time series data point for visualization
/// </summary>
public record TimeSeriesDataPoint(
    DateTimeOffset Timestamp,
    double Value);
