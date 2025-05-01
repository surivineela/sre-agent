// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Azure.Monitor.Query.Models;

namespace Agent.Plugins;

public class AzureMonitorMetricsPlugin : IAzureMonitorMetricsPlugin
{
    private readonly AzureMonitorMetricsHelper _azureMonitorMetricsHelper;
    public Guid? ThreadId { get; set; }

    // Azure-supported granularity buckets
    readonly TimeSpan[] supportedBuckets = new[]
    {
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(12),
        TimeSpan.FromDays(1)
    };

    public AzureMonitorMetricsPlugin(AzureMonitorMetricsHelper azureMonitorMetricsHelper)
    {
        _azureMonitorMetricsHelper = azureMonitorMetricsHelper;
    }

    public async Task<List<MetricDefinition>> ListMetricsForAzureResource(string resourceId)
    {
        return await _azureMonitorMetricsHelper.ListMetricsAsync(resourceId);
    }

    public async Task<IReadOnlyList<MetricTimeSeriesElement>> QueryMetricValuesForAzureResource(
        string resourceId, string metricNamespace, string metricName, DateTimeOffset startTime, DateTimeOffset endTime)
    {
        var duration = endTime - startTime;

        // Calculate minimum granularity to keep results under 1440 points
        var minGranularity = TimeSpan.FromTicks(duration.Ticks / 1440);

        // Pick the first supported bucket >= minGranularity
        var matchingBucket = supportedBuckets.FirstOrDefault(bucket => bucket >= minGranularity);
        var roundedGranularity = matchingBucket != default
            ? matchingBucket
            : supportedBuckets.Last(); // fallback to 1 day if all buckets are smaller

        var metricsQueryResult = await _azureMonitorMetricsHelper.QueryResourceMetricAsync(
            resourceId, metricNamespace, metricName, startTime, endTime, roundedGranularity);

        return metricsQueryResult.Metrics[0].TimeSeries;
    }
}
