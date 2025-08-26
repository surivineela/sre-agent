// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Models;
using Agent.Plugins.Interface;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(Category = ToolCategories.KnowledgeBase)]
    public class AzureApplicationInsightsPluginDefinition : ContextToolTarget<AgentContext>
    {
        public IAzureApplicationInsightsPlugin _plugin { get; }

        public AzureApplicationInsightsPluginDefinition(IAzureApplicationInsightsPlugin azureApplicationInsightsPlugin)
        {
            _plugin = azureApplicationInsightsPlugin;
        }

        [KernelFunction("CorrelateTimeSeries")]
        [Description(
        $$"""
Perform a time-series correlation analysis based on a user-reported symptom on an Application Insights resource.

        This tool takes one or more data sets. Each data set consists of a table, filters and splitBy dimensions. The tool will
        construct time series of each data set split by the splitBy dimensions, then correlate the time series to find
        the most likely causes of the symptom.

        Example data sets:

        Determine which result code is contributing to the symptom:
        [
            {
                "table": "requests",
                "filters": [
                    "success=\"false\""
                 ],
                "splitBy": "resultCode"
            }
        ]

        Determine whether a specific exception is contributing to 500 errors:
        [
            {
                "table": "requests",
                "filters": [
                    "resultCode=\"500\""
                ]
            },
            {
                "table": "exceptions",
                "filters": [
                    "type=\"System.InvalidOperationException\""
                ]
            }
        ]

        Determine which operation name is contributing to slow performance:
        [
            {
                "table": "requests",
                "splitBy": "operation_Name",
                "aggregation": "Average"
            },
            {
                "table": "requests",
                "splitBy": "operation_Name",
                "aggregation": "95thPercentile"
            }
        ]
""")]
        [AgentTool(ToolMode.Auto)]
        public async Task<AppCorrelateTimeResult[]> CorrelateTimeSeries(
            [Description("Azure Resource Id of the application insight resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/169/resourceGroups/myrg/providers/microsoft.insights/components/resourceName")] string resourceId,
            [Description("Query to fetch records. Each data set consists of a table, filters and splitBy dimensions.")] List<AppCorrelateDataSet> dataSets,
            [Description("Start time for the query")] DateTime startTime,
            [Description("End time for the query")] DateTime endTime)
        {
            return await _plugin.CorrelateTimeSeries(resourceId, dataSets, startTime, endTime);
        }


        [KernelFunction("GetDistributedTrace")]
        [Description(
        $$"""
Retrieve the distributed trace for an application based on the TraceId and SpanId.

This tool is useful for identifying the root cause of problems in an application.
and can be used to retrieve the errors, dependency calls and other information about a specific transaction.
""")]
        [AgentTool(ToolMode.Auto)]
        public async Task<DistributedTraceResult> GetDistributedTrace(
            [Description("Azure Resource Id of the application insight resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/169/resourceGroups/myrg/providers/microsoft.insights/components/resourceName")] string resourceId,
            [Description("A unique identifier to filter related telemetry data across different components and services in a distributed application")] string traceId,
            [Description("Optional. A unique identifier to scope down to a single unit of work or operation within a trace")] string? spanId,
            [Description("Start time for the query")] DateTime startTime,
            [Description("End time for the query")] DateTime endTime)
        {
            return await _plugin.GetDistributedTrace(resourceId, traceId, spanId, startTime, endTime);
        }


        [KernelFunction("ListDistributedTraces")]
        [Description(
        $$"""
List the most relevant traces from an Application Insights table.

This tool is useful for correlating errors and dependencies to specific transactions in an application.

Returns a list of traceIds and spanIds that can be further explored for each operation.

Example usage:
Filter to dependency failures
"table": "dependencies",
"filters": ["success=\"false\""]

Filter to request failures with 500 code
"table": "requests",
"filters": ["success=\"false\"", "resultCode=\"500\""]

Filter to requests slower than 95th percentile (use start and end time filters to filter to the duration spike). Any percentile is valid (e.g. 99p is also valid)
"table": "requests",
"filters": ["duration=\"95p\""],
"start-time":"start of spike (ISO date)",
"end-time":"end of spike (ISO date)"
""")]
        [AgentTool(ToolMode.Auto)]
        public async Task<AppListTraceResult> ListDistributedTraces(
            [Description("Azure Resource Id of the application insight resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/169/resourceGroups/myrg/providers/microsoft.insights/components/resourceName")] string resourceId,
            [Description("Array of filters on the provided table.")] string[] filters,
            [Description("Table name to run query on. Valid options are: exceptions, dependencies, availabilityResults, requests.")] string table,
            [Description("Start time for the query")] DateTime startTime,
            [Description("End time for the query")] DateTime endTime)
        {
            return await _plugin.ListDistributedTraces(resourceId, filters, table, startTime, endTime);
        }


        [KernelFunction("GetImpact")]
        [Description(
        $$"""
Evaluate the distribution and impact of an issue impacting an application.

This tool is useful for understanding how many instances are impacted and what the failure rates are.

You can use this to validate how widespread an issue is, or to determine the impact of a specific error code or type of dependency.

Example usage:
Determine how many instances and the overall failure rate caused by requests with a 500 result code:
"table": "requests",
"filters": ["resultCode=\"500\""]

Determine how many instances and the overall failure rate caused by Azure Blob storage 500 errors:
"table": "dependencies",
"filters": ["type=\"Azure Blob\"", "resultCode=\"500\""]
""")]
        [AgentTool(ToolMode.Auto)]
        public async Task<List<AppImpactResult>> GetImpact(
            [Description("Azure Resource Id of the application insight resource to analyze. Should begin with /subscriptions/... Example: /subscriptions/169/resourceGroups/myrg/providers/microsoft.insights/components/resourceName")] string resourceId,
            [Description("Array of filters on the provided table.")] string[] filters,
            [Description("Table name to run query on.  Valid options are: dependencies and requests.")] string table,
            [Description("Start time for the query")] DateTime startTime,
            [Description("End time for the query")] DateTime endTime)
        {
            return await _plugin.GetImpact(resourceId, filters, table, startTime, endTime);
        }
    }
}
