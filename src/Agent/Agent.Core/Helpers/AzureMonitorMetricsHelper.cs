// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Azure.Monitor.Query;
using Azure.Monitor.Query.Models;

namespace Agent.Core.Helpers;

public class AzureMonitorMetricsHelper
{
    private readonly IAuthenticationService _authService;

    public AzureMonitorMetricsHelper(IHttpClientFactory httpClientFactory, IArmClientFactory armClientFactory, IAuthenticationService authService, AzureSettings azureSettings)
    {
        _authService = authService;
    }

    public async Task<List<MetricDefinition>> ListMetricsAsync(string resourceId)
    {
        var client = new MetricsQueryClient(_authService.GetArmOperationCredential());
        var metrics = new List<MetricDefinition>();

        var nsChecked = new HashSet<string>();
        await foreach (var ns in client.GetMetricNamespacesAsync(resourceId))
        {
            if (!nsChecked.Contains(ns.FullyQualifiedName))
            {
                nsChecked.Add(ns.FullyQualifiedName);
                await foreach (var def in client.GetMetricDefinitionsAsync(resourceId, ns.FullyQualifiedName))
                {
                    metrics.Add(def);
                }
            }
        }

        return metrics;
    }

    public async Task<MetricsQueryResult> QueryResourceMetricAsync(
        string resourceId,
        string metricNamespace,
        string metricName,
        DateTimeOffset startTime,
        DateTimeOffset endTime,
        TimeSpan granularity)
    {
        var client = new MetricsQueryClient(_authService.GetArmOperationCredential());

        // TODO: Limit resource metrics data
        var response = await client.QueryResourceAsync(
            resourceId,
            new[] { metricName },
            new MetricsQueryOptions
            {
                TimeRange = new QueryTimeRange(startTime, endTime),
                Granularity = granularity,
                Aggregations = { MetricAggregationType.Average }, // TOOD: Take as input
                MetricNamespace = metricNamespace,
                // TODO: Filter by dimensions
            });

        return response.Value;
    }
}
