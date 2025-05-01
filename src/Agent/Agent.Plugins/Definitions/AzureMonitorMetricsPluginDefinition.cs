// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Azure.Monitor.Query.Models;

namespace Agent.Plugins
{
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
            [Description("The start time (UTC) for the metric query range. Example: Yesterday's date. Default to last 1 day. Validation start date should be within last 90 days")] DateTime startTime,
            [Description("The end time (UTC) for the metric query range. Example: Today's date. default to current time. Validation limit end date from last 90 days")] DateTime endTime)
        //[Description("Dimensions for the metric that can be used to get further insights")] string[] dimensions) // TODO: Add dimension property
        {
            return await _plugin.QueryMetricValuesForAzureResource(resourceId, metricNamespace, metricName, startTime, endTime);
        }
    }
}
