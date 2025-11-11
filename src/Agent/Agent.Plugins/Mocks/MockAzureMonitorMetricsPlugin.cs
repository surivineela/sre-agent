// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Interface;
using Azure.Monitor.Query.Models;

namespace Agent.Plugins.Mocks
{
    public class MockAzureMonitorMetricsPlugin : IAzureMonitorMetricsPlugin
    {
        public MockAzureMonitorMetricsPlugin()
        {
        }

        public Guid? ThreadId { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public Task<List<MetricDefinition>> ListMetricsForAzureResource(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<MetricTimeSeriesElement>> QueryMetricValuesForAzureResource(
            string resourceId,
            string metricNamespace,
            string metricName,
            DateTimeOffset startTime,
            DateTimeOffset endTime,
            string dimensionFilter = "",
            MetricAggregationType aggregationType = MetricAggregationType.Average)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetMetricsTimeSeriesAnalysisAsync(
            string resourceId,
            string metricNamespace,
            string metricName,
            DateTimeOffset startTime,
            DateTimeOffset endTime,
            string dimensionFilter = "",
            string contextualQuery = ""
        )
        {
            throw new NotImplementedException();
        }
    }
}

