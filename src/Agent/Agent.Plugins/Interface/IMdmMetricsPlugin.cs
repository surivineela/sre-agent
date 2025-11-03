// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------


using Agent.Plugins.Models;
using Microsoft.Cloud.Metrics.Client.Query;

namespace Agent.Plugins.Interface
{
    public interface IMdmMetricsPlugin
    {
        Task<string> GetNamespacesAsync(string monitoringAccount);

        Task<MetricDefinition[]> GetMetricAsync(string monitoringAccount, string metricNamespace);

        Task<string[]> GetDimensionNamesAsync(string monitoringAccount, string metricNamespace, string metricName);

        Task<string> GetDimensionValuesAsync(
            string monitoringAccount,
            string metricNamespace,
            string metricName,
            string dimensionName,
            string dimensionFiltersJson,
            string startTimeUtc,
            string endTimeUtc);

        Task<string> GetMultipleTimeSeriesAsync(
            string startTimeUtc,
            string endTimeUtc,
            int seriesResolutionInMinutes,
            string requestJson);

        Task<IQueryResultListV3> GetTimeSeriesAsync(
            string monitoringAccount,
            string metricNamespace,
            string metricName,
            string startTimeUtc,
            string endTimeUtc,
            int seriesResolutionInMinutes = 5,
            string requestJson = "{ \"samplingTypes\": [\"Count\"], \"aggregationType\": \"Automatic\", \"lastValueMode\": false }");

        Task<string> GetTimeSeriesAnalysisAsync(
            string monitoringAccount,
            string metricNamespace,
            string metricName,
            string startTimeUtc,
            string endTimeUtc,
            int seriesResolutionInMinutes = 5,
            string requestJson = "{ \"samplingTypes\": [\"Count\"], \"aggregationType\": \"Automatic\", \"lastValueMode\": false }",
            string? contextualQuery = null);
    }
}
