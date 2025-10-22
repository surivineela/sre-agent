// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins.Interface;
using Agent.Core.Models;
using Agent.Framework;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(Category = ToolCategories.AzureOperation)]
    public class RedisPluginDefinition
    {
        private readonly IRedisPlugin _redisPlugin;

        public RedisPluginDefinition(IRedisPlugin redisPlugin)
        {
            _redisPlugin = redisPlugin;
        }

        [Description("Gets basic Azure Cache for Redis information including SKU, location, and status. " +
            "Use this method to get high-level Redis cache details for initial assessment.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<RedisCacheInfo> GetRedisCacheAsync(
            [Description("The full Azure resource ID of the Redis cache.")] string resourceId)
        {
            return await _redisPlugin.GetRedisCacheAsync(resourceId);
        }

        [Description("Gets detailed Azure Cache for Redis information including configuration, Redis version, ports, and access details. " +
            "Use this method to get comprehensive Redis cache information for detailed analysis.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<RedisCacheDetailedInfo> GetRedisCacheInfoAsync(
            [Description("The full Azure resource ID of the Redis cache.")] string resourceId)
        {
            return await _redisPlugin.GetRedisCacheInfoAsync(resourceId);
        }

        [Description("Gets Redis server load metrics over a specified time window. " +
            "Server load > 80% indicates performance bottleneck requiring workload reduction or scaling. " +
            "Use this to identify high server load issues causing Redis command timeouts.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<RedisServerLoadMetrics> GetRedisServerLoadAsync(
            [Description("The full Azure resource ID of the Redis cache.")] string resourceId,
            [Description("Time window for metrics collection (e.g., TimeSpan.FromMinutes(30)).")] TimeSpan window)
        {
            return await _redisPlugin.GetRedisServerLoadAsync(resourceId, window);
        }

        [Description("Gets Redis connected clients metrics over a specified time window. " +
            "Spikes in connected clients indicate high connection creation rates which can cause performance issues. " +
            "Use this to identify connection pooling problems and connection management issues.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<RedisConnectedClientsMetrics> GetRedisConnectedClientsAsync(
            [Description("The full Azure resource ID of the Redis cache.")] string resourceId,
            [Description("Time window for metrics collection (e.g., TimeSpan.FromMinutes(30)).")] TimeSpan window)
        {
            return await _redisPlugin.GetRedisConnectedClientsAsync(resourceId, window);
        }

        [Description("Gets Redis command latency metrics over a specified time window. " +
            "High latency indicates performance problems that may be causing timeouts. " +
            "Use this to analyze Redis command execution performance and identify latency issues.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<RedisCommandLatencyMetrics> GetRedisCommandLatencyAsync(
            [Description("The full Azure resource ID of the Redis cache.")] string resourceId,
            [Description("Time window for metrics collection (e.g., TimeSpan.FromMinutes(30)).")] TimeSpan window)
        {
            return await _redisPlugin.GetRedisCommandLatencyAsync(resourceId, window);
        }

        [Description("Gets Redis memory usage metrics over a specified time window. " +
            "High memory usage can cause evictions and performance degradation. " +
            "Use this to analyze memory pressure and identify when scaling is needed.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<RedisMemoryUsageMetrics> GetRedisMemoryUsageAsync(
            [Description("The full Azure resource ID of the Redis cache.")] string resourceId,
            [Description("Time window for metrics collection (e.g., TimeSpan.FromMinutes(30)).")] TimeSpan window)
        {
            return await _redisPlugin.GetRedisMemoryUsageAsync(resourceId, window);
        }

        [Description("Gets Redis eviction metrics over a specified time window. " +
            "High eviction rates indicate memory pressure requiring larger cache size or data optimization. " +
            "Use this to analyze memory pressure and eviction patterns affecting cache performance.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<RedisEvictionMetrics> GetRedisEvictionMetricsAsync(
            [Description("The full Azure resource ID of the Redis cache.")] string resourceId,
            [Description("Time window for metrics collection (e.g., TimeSpan.FromMinutes(30)).")] TimeSpan window)
        {
            return await _redisPlugin.GetRedisEvictionMetricsAsync(resourceId, window);
        }

        [Description("Gets Redis cache hit/miss ratio metrics over a specified time window. " +
            "Poor hit ratios indicate inefficient cache usage or inappropriate eviction policies. " +
            "Use this to analyze cache effectiveness and optimize data access patterns.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<RedisHitMissRatioMetrics> GetRedisHitMissRatioAsync(
            [Description("The full Azure resource ID of the Redis cache.")] string resourceId,
            [Description("Time window for metrics collection (e.g., TimeSpan.FromMinutes(30)).")] TimeSpan window)
        {
            return await _redisPlugin.GetRedisHitMissRatioAsync(resourceId, window);
        }
    }
}
