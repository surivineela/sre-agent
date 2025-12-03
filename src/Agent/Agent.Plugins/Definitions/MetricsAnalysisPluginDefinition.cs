// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Agent.Plugins.SampleData;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(Category = ToolCategories.Monitoring)]
    public class MetricsAnalysisPluginDefinition
    {
        private readonly IMetricsAnalysisPlugin _metricsAnalysisPlugin;
        private readonly IEnumerable<IMetricProviderHandler> _metricProviderHandlers;

        public MetricsAnalysisPluginDefinition(
            IMetricsAnalysisPlugin metricsAnalysisPlugin,
            IEnumerable<IMetricProviderHandler> metricProviderHandlers)
        {
            _metricsAnalysisPlugin = metricsAnalysisPlugin;
            _metricProviderHandlers = metricProviderHandlers;
        }

        [Description("Discovers and returns all available metrics with their names and descriptions from a specific metric provider. " +
            "This helps identify what metrics are available for analysis before querying specific time series data.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<MetricDefinition[]> DiscoverMetricsAsync(
            [Description("Name of the metric provider. Supported values: 'AzureMonitor', 'Geneva', 'Prometheus'")]
            string metricProvider,
            [Description("Optional resource identifier to scope the metrics discovery (e.g., Azure Resource ID for Azure Monitor)")]
            string? resourceId)
        {
            // Resolve the handler from the metricProvider parameter
            var handler = _metricProviderHandlers.FirstOrDefault(h =>
                h.ProviderName.Equals(metricProvider, StringComparison.OrdinalIgnoreCase));

            if (handler == null)
            {
                throw new InvalidOperationException($"Metric provider '{metricProvider}' not found or not supported.");
            }

            // Call the handler's method to discover available metrics
            return await handler.DiscoverMetricsAsync(resourceId);
        }

        [Description("Gets the list of available dimension names for a specific metric. " +
            "Dimensions allow filtering and grouping of metric data (e.g., by instance, status code, region).")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string[]> GetDimensionNamesAsync(
            [Description("Name of the metric provider. Supported values: 'AzureMonitor', 'Geneva', 'Prometheus'")]
            string metricProvider,
            [Description("Name of the metric to get dimensions for (e.g., 'CPU_Usage', 'Memory_Available_Bytes')")]
            string metricName,
            [Description("Optional resource identifier to scope the dimension query (e.g., Azure Resource ID for Azure Monitor)")]
            string? resourceId)
        {
            // Resolve the handler from the metricProvider parameter
            var handler = _metricProviderHandlers.FirstOrDefault(h =>
                h.ProviderName.Equals(metricProvider, StringComparison.OrdinalIgnoreCase));

            if (handler == null)
            {
                throw new InvalidOperationException($"Metric provider '{metricProvider}' not found or not supported.");
            }

            // Call the handler's method to get dimension names
            return await handler.GetDimensionNamesAsync(metricName, resourceId);
        }

        [Description("Performs comprehensive metrics analysis using three approaches: " +
            "1) Direct LLM analysis for general insights, " +
            "2) Statistical/ML analysis using Python code generation and execution, " +
            "3) Visualization analysis using line charts and multimodal LLM. " +
            "Returns factual observations based on the combined analysis.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> AnalyzeMetricsTestAsync(
            [Description("Description of the symptoms being investigated (e.g., 'High CPU usage and slow response times')")]
            string symptoms)
        {
            var mocktimeSeries = new[] { MetricsSampleData.ResponseLatencyWithSpikes };
            var result = await _metricsAnalysisPlugin.AnalyzeMetricsAsync(symptoms, mocktimeSeries);
            return result.CombinedAnalysis;
        }

        [Description("Analyzes metrics by querying specific metric data with time range and aggregation type. " +
            "This tool automatically generates dimension filters using LLM based on the provided symptoms and resource details. " +
            "Performs comprehensive metrics analysis using three approaches: " +
            "1) Direct LLM analysis for general insights, " +
            "2) Statistical/ML analysis using Python code generation and execution, " +
            "3) Visualization analysis using line charts and multimodal LLM. " +
            "Returns factual observations based on the combined analysis. " +
            "Use AnalyzeMetricsWithFilter if you need explicit control over dimension filters.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> AnalyzeMetricsAsync(
            [Description("Description of the symptoms being investigated (e.g., 'High CPU usage and slow response times')")]
            string symptoms,
            [Description("Details of the impacted resource including resource id, region, and other properties (e.g., 'Resource ID: /subscriptions/.../resourceGroups/myRG/providers/Microsoft.Compute/virtualMachines/myVM, Region: East US, SKU: Standard_D4s_v3')")]
            string resourceDetails,
            [Description("Optional resource identifier to scope the metric query (e.g., Azure Resource ID for Azure Monitor)")]
            string? resourceId,
            [Description("Name of the metric provider. Supported values: 'AzureMonitor', 'Geneva', 'Prometheus'")]
            string metricProvider,
            [Description("Name of the metric to query (e.g., 'CPU_Usage', 'Memory_Available_Bytes')")]
            string metricName,
            [Description("Start time for the metric query in ISO 8601 format (e.g., '2024-01-01T00:00:00+04:00')")]
            DateTime startTime,
            [Description("End time for the metric query in ISO 8601 format (e.g., '2024-01-01T23:59:59+04:00')")]
            DateTime endTime,
            [Description("Aggregation type to apply to the metric data. Supported values: 'Average', 'Sum', 'Min', 'Max', 'Count'")]
            AggregationType aggregation)
        {
            // Resolve the handler from the metricProvider parameter
            var handler = _metricProviderHandlers.FirstOrDefault(h =>
                h.ProviderName.Equals(metricProvider, StringComparison.OrdinalIgnoreCase));

            if (handler == null)
            {
                throw new InvalidOperationException($"Metric provider '{metricProvider}' not found or not supported.");
            }

            var dimensions = await handler.GetDimensionNamesAsync(metricName, resourceId);
            var generatedFilters = await _metricsAnalysisPlugin.GenerateFiltersAsync(symptoms, resourceDetails, metricName, dimensions);

            TimeSeries[] timeSeries = await handler.FetchMetricDataAsync(metricName, startTime.ToUniversalTime(), endTime.ToUniversalTime(), generatedFilters, aggregation, resourceId);
            var result = await _metricsAnalysisPlugin.AnalyzeMetricsAsync(symptoms, timeSeries);
            return result.CombinedAnalysis;
        }

        [Description("Analyzes metrics with explicit dimension filters provided by the agent. " +
            "Use this when you want to specify your own dimension filters instead of having them auto-generated. " +
            "Performs comprehensive metrics analysis using three approaches: " +
            "1) Direct LLM analysis for general insights, " +
            "2) Statistical/ML analysis using Python code generation and execution, " +
            "3) Visualization analysis using line charts and multimodal LLM. " +
            "Returns factual observations based on the combined analysis.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> AnalyzeMetricsWithFilterAsync(
            [Description("Description of the symptoms being investigated (e.g., 'High CPU usage and slow response times')")]
            string symptoms,
            [Description("Optional resource identifier to scope the metric query (e.g., Azure Resource ID for Azure Monitor)")]
            string? resourceId,
            [Description("Name of the metric provider. Supported values: 'AzureMonitor', 'Geneva', 'Prometheus'")]
            string metricProvider,
            [Description("Name of the metric to query (e.g., 'CPU_Usage', 'Memory_Available_Bytes')")]
            string metricName,
            [Description("Start time for the metric query in ISO 8601 format (e.g., '2024-01-01T00:00:00+04:00')")]
            DateTime startTime,
            [Description("End time for the metric query in ISO 8601 format (e.g., '2024-01-01T23:59:59+04:00')")]
            DateTime endTime,
            [Description("Aggregation type to apply to the metric data. Supported values: 'Average', 'Sum', 'Min', 'Max', 'Count'")]
            AggregationType aggregation,
            [Description("Optional dimension filters to apply to the metric query. Format: Dictionary where key is dimension name and value is dimension value. " +
                "Examples: {'instanceId': 'instance-1'}, {'statusCode': '500', 'apiName': 'GetBlob'}. " +
                "Use '*' as value to split by that dimension (e.g., {'instanceId': '*'} returns separate series for each instance).")]
            Dictionary<string, string>? dimensionFilters = null)
        {
            // Resolve the handler from the metricProvider parameter
            var handler = _metricProviderHandlers.FirstOrDefault(h =>
                h.ProviderName.Equals(metricProvider, StringComparison.OrdinalIgnoreCase));

            if (handler == null)
            {
                throw new InvalidOperationException($"Metric provider '{metricProvider}' not found or not supported.");
            }

            // Convert Dictionary to DimensionFilter array
            DimensionFilter[] filters = dimensionFilters?.Select(kv => new DimensionFilter(kv.Key, kv.Value)).ToArray() ?? Array.Empty<DimensionFilter>();

            TimeSeries[] timeSeries = await handler.FetchMetricDataAsync(metricName, startTime.ToUniversalTime(), endTime.ToUniversalTime(), filters, aggregation, resourceId);
            var result = await _metricsAnalysisPlugin.AnalyzeMetricsAsync(symptoms, timeSeries);
            return result.CombinedAnalysis;
        }
    }
}
