// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Plugins.Interface;
using Azure.Monitor.Query.Models;
using Microsoft.OperationalAgent.Core.Extensions;

namespace Agent.Plugins
{
    [AgentToolPlugin(Category = ToolCategories.LogQuery)]
    public class AzureMonitorMetricsPluginDefinition
    {
        public IAzureMonitorMetricsPlugin _plugin { get; }

        public AzureMonitorMetricsPluginDefinition(IAzureMonitorMetricsPlugin azureMonitorMetricsPlugin)
        {
            _plugin = azureMonitorMetricsPlugin;
        }

        [Description("Lists all available metric definitions for a given Azure resource. Returns MetricDefinition object which contains properties like Name, Unit, DisplayDescription, Dimensions.")]
        public async Task<List<MetricDefinition>> ListAvailableMetrics(
            [Description("Azure Resource Id of the resource, e.g., /subscriptions/xxx/resourceGroups/yyy/providers/Microsoft.Web/sites/myapp")] string resourceId)
        {
            return await _plugin.ListMetricsForAzureResource(resourceId);
        }

        [Description("Get time-series metric values for a specific metric name of a azure resource id. Returns metric records for the start time and end time provided using 'Average' aggregation with the interval value inputed. Use chart plugin to render visual where possible")]
        public async Task<IReadOnlyList<MetricTimeSeriesElement>> GetMetricTimeSeriesElementsForAzureResource(
            [Description("Azure Resource Id of the resource, e.g., /subscriptions/xxx/resourceGroups/yyy/providers/Microsoft.Web/sites/myapp")] string resourceId,
            [Description("Fully qualified metric namespace from MetricDefinition.FullyQualifiedName property. Generally it is Azure Resource Type for which metric is being fetched (e.g., Microsoft.Web/sites)")] string metricNamespace,
            [Description("The metric name to query (e.g., CpuUsage, Requests, etc.). Must match the name returned from ListAvailableMetrics.")] string metricName,
            [Description("The start time for the metric query range (Absolute in UTC or relative). Examples: '2024-03-05 10:50:00', '20 hours ago', '3 days ago'. Prefer relative format for recent values (e.g: '24 hours ago', '2 days ago'). Validation start date should be within last 90 days")] string startTime,
            [Description("The end time for the metric query range (Absolute in UTC or relative). Examples: '2024-03-05 10:50:00', 'now', 'an hour ago'. Prefer relative format for recent value (e.g: 'now', '1 hour ago'). Validation limit end date from last 90 days")] string endTime,
            [Description("Optional dimension filter in OData syntax. Use 'dimension eq \'value\'' format for exact match or 'dimension eq \'*\'' to split by dimension. Multiple conditions can be combined with 'and'. Examples: statusCode eq \'200\', revisionName eq \'*\', " +
            "statusCode eq \'200\' and revisionName eq \'rev-1\'. Available dimensions can be found in the LocalizedDimensions property of the MetricDefinition.")] string dimensionFilter = "")
        {
            // Note: startTime and endTime are inputs from the LLM. I don't think it has a concept of moving time.
            // For example: it insists that 'now' (May '25) is some random day middle of July '24.
            // Unless this is an investigation where the LLM is using absolute date/time ranges, we should instruct it to prefer relative time ranges to now, then we can calculate them more accurately.
            var now = DateTimeOffset.UtcNow;

            return await _plugin.QueryMetricValuesForAzureResource(
                resourceId,
                metricNamespace,
                metricName,
                startTime.RecognizeAsDateTime() ?? now.AddDays(-1),
                endTime.RecognizeAsDateTime() ?? now,
                dimensionFilter);
        }
    }
}
