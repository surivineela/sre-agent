// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Monitor.Query.Models;

namespace Agent.Plugins.Interface
{
    public interface IAzureMonitorMetricsPlugin
    {
        public Guid? ThreadId { get; set; }

        Task<List<MetricDefinition>> ListMetricsForAzureResource(string resourceId);

        Task<IReadOnlyList<MetricTimeSeriesElement>> QueryMetricValuesForAzureResource(
            string resourceId, string metricNamespace, string metricName, DateTimeOffset startTime, DateTimeOffset endTime, string dimensionFilter = "");

        Task<string> GetMetricsTimeSeriesAnalysisAsync(
            string resourceId,
            string metricNamespace,
            string metricName,
            DateTimeOffset startTime,
            DateTimeOffset endTime,
            string dimensionFilter = "",
            string contextualQuery = "");
    }
}
