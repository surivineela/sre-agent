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
            string metricProvider)
        {
            // Resolve the handler from the metricProvider parameter
            var handler = _metricProviderHandlers.FirstOrDefault(h =>
                h.ProviderName.Equals(metricProvider, StringComparison.OrdinalIgnoreCase));

            if (handler == null)
            {
                throw new InvalidOperationException($"Metric provider '{metricProvider}' not found or not supported.");
            }

            // Call the handler's method to discover available metrics
            return await handler.DiscoverMetricsAsync();
        }

        [Description("Performs comprehensive metrics analysis using three approaches: " +
            "1) Direct LLM analysis for general insights, " +
            "2) Statistical/ML analysis using Python code generation and execution, " +
            "3) Visualization analysis using line charts and multimodal LLM (GPT-4o). " +
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

        [Description("Analyzes metrics by querying specific metric data with time range, and aggregation type." +
            "Performs comprehensive metrics analysis using three approaches: " +
            "1) Direct LLM analysis for general insights, " +
            "2) Statistical/ML analysis using Python code generation and execution, " +
            "3) Visualization analysis using line charts and multimodal LLM (GPT-4o). " +
            "Returns factual observations based on the combined analysis.")]
        [AgentTool(ToolMode.Auto)]
        public async Task<string> AnalyzeMetricsAsync(
            [Description("Description of the symptoms being investigated (e.g., 'High CPU usage and slow response times')")]
            string symptoms,
            [Description("Details of the impacted resource including resource id, region, and other properties (e.g., 'Resource ID: /subscriptions/.../resourceGroups/myRG/providers/Microsoft.Compute/virtualMachines/myVM, Region: East US, SKU: Standard_D4s_v3')")]
            string resourceDetails,
            [Description("Name of the metric provider. Supported values: 'AzureMonitor', 'Geneva', 'Prometheus'")]
            string metricProvider,
            [Description("Name of the metric to query (e.g., 'CPU_Usage', 'Memory_Available_Bytes')")]
            string metricName,
            [Description("Start time for the metric query in ISO 8601 format (e.g., '2024-01-01T00:00:00Z')")]
            DateTime startTime,
            [Description("End time for the metric query in ISO 8601 format (e.g., '2024-01-01T23:59:59Z')")]
            DateTime endTime,
            [Description("Aggregation type to apply to the metric data (e.g., 'Average', 'Sum', 'Maximum', 'Minimum', 'Count')")]
            string aggregation)
        {
            // Resolve the handler from the metricProvider parameter
            var handler = _metricProviderHandlers.FirstOrDefault(h =>
                h.ProviderName.Equals(metricProvider, StringComparison.OrdinalIgnoreCase));

            if (handler == null)
            {
                throw new InvalidOperationException($"Metric provider '{metricProvider}' not found or not supported.");
            }

            var dimensions = await handler.GetDimensionNamesAsync(metricName);
            var generatedFilters = await _metricsAnalysisPlugin.GenerateFiltersAsync(symptoms, resourceDetails, metricName, dimensions);

            TimeSeries[] timeSeries = await handler.FetchMetricDataAsync(metricName, startTime, endTime, generatedFilters, aggregation);
            var result = await _metricsAnalysisPlugin.AnalyzeMetricsAsync(symptoms, timeSeries);
            return result.CombinedAnalysis;
        }
    }
}
