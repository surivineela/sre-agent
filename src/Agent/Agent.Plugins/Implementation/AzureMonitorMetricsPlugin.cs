// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Helpers;
using Agent.Plugins.Interface;
using Azure.Core;
using Azure.Monitor.Query.Models;

namespace Agent.Plugins;

public class AzureMonitorMetricsPlugin : IAzureMonitorMetricsPlugin
{
    private readonly AzureMonitorMetricsHelper _azureMonitorMetricsHelper;
    public Guid? ThreadId { get; set; }

    // In-memory cache: key is "resourceType", value is List<MetricDefinition>
    private static readonly ConcurrentDictionary<string, List<MetricDefinition>> _metricDefinitionsCache = new(StringComparer.OrdinalIgnoreCase);

    public AzureMonitorMetricsPlugin(AzureMonitorMetricsHelper azureMonitorMetricsHelper)
    {
        _azureMonitorMetricsHelper = azureMonitorMetricsHelper;
    }

    public async Task<List<MetricDefinition>> ListMetricsForAzureResource(string resourceId)
    {
        var resourceIdentifier = new ResourceIdentifier(resourceId);
        var cacheKey = resourceIdentifier.ResourceType.ToString().ToLowerInvariant();

        if (_metricDefinitionsCache.TryGetValue(cacheKey, out var cachedMetrics))
        {
            return cachedMetrics;
        }

        var metrics = await _azureMonitorMetricsHelper.ListMetricsAsync(resourceId);
        _metricDefinitionsCache[cacheKey] = metrics;
        return metrics;
    }

    public async Task<IReadOnlyList<MetricTimeSeriesElement>> QueryMetricValuesForAzureResource(
        string resourceId, string metricNamespace, string metricName, DateTimeOffset startTime, DateTimeOffset endTime, string dimensionFilter = "")
    {
        var duration = endTime - startTime;

        // Calculate minimum granularity to keep results under 1440 points
        var minGranularity = TimeSpan.FromTicks(duration.Ticks / 1440);

        var supportedBuckets = await GetSupportedBucketsAsync(resourceId);

        // Pick the first supported bucket >= minGranularity
        var matchingBucket = supportedBuckets.FirstOrDefault(bucket => bucket >= minGranularity);
        var roundedGranularity = matchingBucket != default
            ? matchingBucket
            : supportedBuckets.Last(); // fallback to 1 day if all buckets are smaller

        var metricsQueryResult = await _azureMonitorMetricsHelper.QueryResourceMetricAsync(
            resourceId, metricNamespace, metricName, startTime, endTime, roundedGranularity, dimensionFilter);

        return metricsQueryResult.Metrics[0].TimeSeries;
    }

    private async Task<List<TimeSpan>> GetSupportedBucketsAsync(string resourceId)
    {
        // Get all supported buckets for the metric
        var metricDefinition = await ListMetricsForAzureResource(resourceId);
        var supportedBuckets = metricDefinition
            .SelectMany(md => md.MetricAvailabilities)
            .Where(ma => ma.Granularity != null && ma.Granularity.HasValue && ma.Granularity != TimeSpan.Zero)
            .Select(ma => ma.Granularity!.Value)
            .Distinct()
            .ToList();

        if (!supportedBuckets.Any())
        {
            throw new InvalidOperationException("No supported buckets found for the metric.");
        }

        return supportedBuckets;
    }
}
