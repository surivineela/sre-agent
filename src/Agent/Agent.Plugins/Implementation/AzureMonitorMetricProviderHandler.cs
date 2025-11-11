// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using AzureMetricDefinition = Azure.Monitor.Query.Models.MetricDefinition;
using AzureMetricTimeSeriesElement = Azure.Monitor.Query.Models.MetricTimeSeriesElement;
using AzureMetricAggregationType = Azure.Monitor.Query.Models.MetricAggregationType;
using TimeSeriesDataPoint = Agent.Plugins.Models.TimeSeriesDataPoint;

namespace Agent.Plugins.Implementation
{
    /// <summary>
    /// Handler for fetching metrics from Azure Monitor
    /// </summary>
    public class AzureMonitorMetricProviderHandler : IMetricProviderHandler
    {
        private readonly IAzureMonitorMetricsPlugin _azureMonitorMetricsPlugin;

        public AzureMonitorMetricProviderHandler(
            IAzureMonitorMetricsPlugin azureMonitorMetricsPlugin)
        {
            _azureMonitorMetricsPlugin = azureMonitorMetricsPlugin ?? throw new ArgumentNullException(nameof(azureMonitorMetricsPlugin));
        }

        public string ProviderName => "AzureMonitor";

        /// <summary>
        /// Discovers and returns all available metric names from Azure Monitor for the configured resource
        /// </summary>
        /// <returns>Array of metric definitions available in Azure Monitor</returns>
        public async Task<MetricDefinition[]> DiscoverMetricsAsync(string? resourceId = null)
        {
            if (resourceId == null)
            {
                throw new ArgumentException("Resource ID must be provided for Azure Monitor metrics discovery.", nameof(resourceId));
            }

            // Get metric definitions from Azure Monitor
            List<AzureMetricDefinition> azureMetricDefinitions = await _azureMonitorMetricsPlugin.ListMetricsForAzureResource(resourceId);

            // Convert Azure Monitor MetricDefinition to our generic MetricDefinition
            return azureMetricDefinitions
                .Select(md => new MetricDefinition(md.Name, md.DisplayDescription ?? string.Empty))
                .ToArray();
        }

        /// <summary>
        /// Gets the dimension names for a specific metric from Azure Monitor
        /// </summary>
        /// <param name="metricName">Name of the metric to get dimensions for</param>
        /// <returns>Array of dimension names available for the specified metric</returns>
        public async Task<string[]> GetDimensionNamesAsync(string metricName, string? resourceId = null)
        {
            if (resourceId == null)
            {
                throw new ArgumentException("Resource ID must be provided for Azure Monitor metrics.", nameof(resourceId));
            }

            // Get all metric definitions
            List<AzureMetricDefinition> azureMetricDefinitions = await _azureMonitorMetricsPlugin.ListMetricsForAzureResource(resourceId);

            // Find the specific metric
            AzureMetricDefinition? metricDefinition = azureMetricDefinitions.FirstOrDefault(md => md.Name == metricName);
            if (metricDefinition == null)
            {
                return Array.Empty<string>();
            }

            // Extract dimension names from Dimensions collection
            // Dimensions is a collection of dimension names
            if (metricDefinition.Dimensions != null && metricDefinition.Dimensions.Count > 0)
            {
                return metricDefinition.Dimensions.ToArray();
            }

            return Array.Empty<string>();
        }

        /// <summary>
        /// Fetches time series data from Azure Monitor
        /// </summary>
        /// <param name="metricName">Name of the metric to query</param>
        /// <param name="startTime">Start time for the query</param>
        /// <param name="endTime">End time for the query</param>
        /// <param name="filters">Array of dimension filters</param>
        /// <param name="aggregation">Aggregation type to apply</param>
        /// <returns>Array of time series data</returns>
        public async Task<TimeSeries[]> FetchMetricDataAsync(
            string metricName,
            DateTime startTime,
            DateTime endTime,
            DimensionFilter[] filters,
            AggregationType aggregation,
            string? resourceId = null)
        {
            if (resourceId == null)
            {
                throw new ArgumentException("Resource ID must be provided for Azure Monitor metrics.", nameof(resourceId));
            }

            // Get all metric definitions to find the namespace
            List<AzureMetricDefinition> azureMetricDefinitions = await _azureMonitorMetricsPlugin.ListMetricsForAzureResource(resourceId);
            AzureMetricDefinition? metricDefinition = azureMetricDefinitions.FirstOrDefault(md => md.Name == metricName);

            if (metricDefinition == null)
            {
                throw new InvalidOperationException($"Metric '{metricName}' not found in Azure Monitor for resource '{resourceId}'");
            }

            string metricNamespace = metricDefinition.Namespace ?? string.Empty;

            // Build dimension filter string for Azure Monitor
            // Azure Monitor expects format like: "dimension1 eq 'value1' and dimension2 eq 'value2'"
            string dimensionFilter = string.Empty;
            if (filters != null && filters.Length > 0)
            {
                dimensionFilter = string.Join(" and ",
                    filters.Select(f => $"{f.Dimension} eq '*'"));
            }

            // Query the metric data
            var timeSeriesElements = await _azureMonitorMetricsPlugin.QueryMetricValuesForAzureResource(
                resourceId,
                metricNamespace,
                metricName,
                new DateTimeOffset(startTime),
                new DateTimeOffset(endTime),
                dimensionFilter,
                MapToAzureMetricAggregationType(aggregation));

            // Convert to our generic TimeSeries format
            return ConvertAzureMonitorTimeSeriesElements(timeSeriesElements, metricName, metricDefinition.Unit?.ToString() ?? string.Empty, aggregation);
        }

        /// <summary>
        /// Converts Azure Monitor MetricTimeSeriesElement to our generic TimeSeries format
        /// </summary>
        private TimeSeries[] ConvertAzureMonitorTimeSeriesElements(
            IReadOnlyList<AzureMetricTimeSeriesElement> timeSeriesElements,
            string metricName,
            string? unit,
            AggregationType aggregation)
        {
            if (timeSeriesElements == null || timeSeriesElements.Count == 0)
            {
                return Array.Empty<TimeSeries>();
            }

            var result = new List<TimeSeries>();

            foreach (var element in timeSeriesElements)
            {
                // Extract dimensions from metadata
                var dimensions = new Dictionary<string, string>();
                if (element.Metadata != null)
                {
                    foreach (var kvp in element.Metadata)
                    {
                        dimensions[kvp.Key] = kvp.Value;
                    }
                }

                // Convert data points
                var dataPoints = new List<TimeSeriesDataPoint>();
                foreach (var value in element.Values)
                {
                    // Get the appropriate aggregated value based on the requested aggregation
                    double? metricValue = aggregation switch
                    {
                        AggregationType.Average => value.Average,
                        AggregationType.Sum => value.Total,
                        AggregationType.Max => value.Maximum,
                        AggregationType.Min => value.Minimum,
                        AggregationType.Count => value.Count,
                        _ => value.Average
                    };

                    if (metricValue.HasValue)
                    {
                        dataPoints.Add(new TimeSeriesDataPoint(
                            value.TimeStamp.DateTime,
                            metricValue.Value));
                    }
                }

                result.Add(new TimeSeries(
                    metricName,
                    unit ?? string.Empty,
                    aggregation,
                    dimensions,
                    dataPoints));
            }

            return result.ToArray();
        }

        /// <summary>
        /// Maps our generic AggregationType enum to Azure Monitor's MetricAggregationType enum
        /// </summary>
        private static AzureMetricAggregationType MapToAzureMetricAggregationType(AggregationType aggregation)
        {
            return aggregation switch
            {
                AggregationType.Average => AzureMetricAggregationType.Average,
                AggregationType.Sum => AzureMetricAggregationType.Total,
                AggregationType.Max => AzureMetricAggregationType.Maximum,
                AggregationType.Min => AzureMetricAggregationType.Minimum,
                AggregationType.Count => AzureMetricAggregationType.Count,
                _ => AzureMetricAggregationType.Average
            };
        }
    }
}
