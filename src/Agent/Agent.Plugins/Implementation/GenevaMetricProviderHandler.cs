// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Framework;
using Agent.Plugins.Connector;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Microsoft.Cloud.Metrics.Client.Query;
using MdmSamplingType = Microsoft.Cloud.Metrics.Client.Metrics.SamplingType;
using TimeSeriesDataPoint = Agent.Plugins.Models.TimeSeriesDataPoint;

namespace Agent.Plugins.Implementation
{
    /// <summary>
    /// Handler for fetching metrics from Geneva (MDM)
    /// </summary>
    public class GenevaMetricProviderHandler : IMetricProviderHandler
    {
        private readonly IConnectorResolver _connectorResolver;
        private readonly IMdmMetricsPlugin _mdmMetricsPlugin;

        public GenevaMetricProviderHandler(
            IConnectorResolver connectorResolver,
            IMdmMetricsPlugin mdmMetricsPlugin)
        {
            _connectorResolver = connectorResolver ?? throw new ArgumentNullException(nameof(connectorResolver));
            _mdmMetricsPlugin = mdmMetricsPlugin ?? throw new ArgumentNullException(nameof(mdmMetricsPlugin));
        }

        public string ProviderName => "Geneva";

        /// <summary>
        /// Discovers and returns all available metric names from the Geneva Metrics provider
        /// </summary>
        /// <returns>Array of metric names available in the Geneva Metrics provider</returns>
        public async Task<MetricDefinition[]> DiscoverMetricsAsync()
        {
            var connector = GetGenevaMetricsConnector();

            // Call the MDM plugin to get metric names
            var metricDefinitions = await _mdmMetricsPlugin.GetMetricAsync(
                connector.MonitoringAccount,
                connector.MetricNamespace);
            return metricDefinitions;
        }

        /// <summary>
        /// Gets the dimension names for a specific metric from the Geneva Metrics provider
        /// </summary>
        /// <param name="metricName">Name of the metric to get dimensions for</param>
        /// <returns>Array of dimension names available for the specified metric</returns>
        public async Task<string[]> GetDimensionNamesAsync(string metricName)
        {
            var connector = GetGenevaMetricsConnector();

            // Call the MDM plugin to get dimension names
            var dimensionNames = await _mdmMetricsPlugin.GetDimensionNamesAsync(
                connector.MonitoringAccount,
                connector.MetricNamespace,
                metricName);
            return dimensionNames;
        }

        public async Task<TimeSeries[]> FetchMetricDataAsync(
            string metricName,
            DateTime startTime,
            DateTime endTime,
            DimensionFilter[] filters,
            string aggregation)
        {
            var connector = GetGenevaMetricsConnector();

            // Read monitoring account and namespace from the connector
            string monitoringAccount = connector.MonitoringAccount;
            string metricNamespace = connector.MetricNamespace;

            // Convert aggregation string to sampling type
            string samplingType = ConvertAggregationToSamplingType(aggregation);

            // Build dimension filters for MDM query
            var dimensionFilters = filters
                .Select(kvp => new { dimension = kvp.Dimension, values = new[] { kvp.Value } })
                .ToList();

            // Build request JSON for MDM query
            var requestObject = new
            {
                samplingTypes = new[] { samplingType },
                aggregationType = "Automatic",
                dimensionFilters = dimensionFilters,
                outputDimensionNames = dimensionFilters.Select(df => df.dimension).ToArray(),
                lastValueMode = false
            };

            string requestJson = JsonSerializer.Serialize(requestObject);

            // Call the actual MDM API via the plugin
            var mdmResponse = await _mdmMetricsPlugin.GetTimeSeriesAsync(
                monitoringAccount,
                metricNamespace,
                metricName,
                startTime.ToString("o"),
                endTime.ToString("o"),
                5, // default resolution in minutes
                requestJson);

            // Convert MDM response to TimeSeries format
            return ConvertMdmResponseToTimeSeries(mdmResponse, metricName, aggregation);
        }

        /// <summary>
        /// Converts aggregation type to MDM sampling type
        /// </summary>
        private string ConvertAggregationToSamplingType(string aggregation)
        {
            return aggregation.ToLowerInvariant() switch
            {
                "average" => "Average",
                "sum" => "Sum",
                "maximum" => "Maximum",
                "minimum" => "Minimum",
                "count" => "Count",
                _ => "Average"
            };
        }

        /// <summary>
        /// Converts IQueryResultListV3 to TimeSeries array
        /// </summary>
        private TimeSeries[] ConvertMdmResponseToTimeSeries(IQueryResultListV3 mdmResponse, string metricName, string aggregation)
        {
            if (mdmResponse == null || mdmResponse.Results == null)
            {
                return Array.Empty<TimeSeries>();
            }

            var result = new List<TimeSeries>();
            var aggregationMethod = ConvertAggregationToEnum(aggregation);

            // Convert aggregation string to SamplingType enum
            var samplingType = ConvertAggregationToSamplingTypeEnum(aggregation);

            // Calculate timestamps based on start time and resolution
            var timestamps = new List<DateTime>();
            var currentTime = mdmResponse.StartTimeUtc;
            while (currentTime <= mdmResponse.EndTimeUtc)
            {
                timestamps.Add(currentTime);
                currentTime = currentTime.AddMinutes(mdmResponse.TimeResolutionInMinutes);
            }

            // Process each query result (each represents a single time series)
            foreach (var queryResult in mdmResponse.Results)
            {
                if (queryResult == null)
                {
                    continue;
                }

                // Get the time series values for the requested sampling type
                var values = queryResult.GetTimeSeriesValues(samplingType);
                if (values == null)
                {
                    continue;
                }

                var dataPoints = new List<TimeSeriesDataPoint>();

                // Combine timestamps with values
                for (int i = 0; i < Math.Min(timestamps.Count, values.Length); i++)
                {
                    // Skip NaN values (sentinel for no metric value)
                    if (!double.IsNaN(values[i]))
                    {
                        dataPoints.Add(new TimeSeriesDataPoint(timestamps[i], values[i]));
                    }
                }

                result.Add(new TimeSeries(metricName, string.Empty, aggregationMethod, queryResult.DimensionList.ToDictionary(), dataPoints));
            }

            return result.ToArray();
        }

        /// <summary>
        /// Converts aggregation string to SamplingType enum for MDM queries
        /// </summary>
        private MdmSamplingType ConvertAggregationToSamplingTypeEnum(string aggregation)
        {
            return aggregation.ToLowerInvariant() switch
            {
                "average" => MdmSamplingType.Average,
                "sum" => MdmSamplingType.Sum,
                "maximum" => MdmSamplingType.Max,
                "minimum" => MdmSamplingType.Min,
                "count" => MdmSamplingType.Count,
                _ => MdmSamplingType.Average
            };
        }

        /// <summary>
        /// Converts aggregation string to AggregationMethod enum
        /// </summary>
        private AggregationMethod ConvertAggregationToEnum(string aggregation)
        {
            return aggregation.ToLowerInvariant() switch
            {
                "average" => AggregationMethod.Average,
                "sum" => AggregationMethod.Sum,
                "maximum" => AggregationMethod.Max,
                "minimum" => AggregationMethod.Min,
                "count" => AggregationMethod.Count,
                _ => AggregationMethod.Average
            };
        }

        /// <summary>
        /// Gets the Geneva Metrics connector from settings
        /// </summary>
        /// <returns>The configured GenevaMetricsConnector</returns>
        /// <exception cref="InvalidOperationException">Thrown when the connector is not found</exception>
        private GenevaMetricsConnector GetGenevaMetricsConnector()
        {
            var connector = _connectorResolver.GetConnectorFromSettings<GenevaMetricsConnector>(
                connectorName: string.Empty,
                connectorType: "GenevaMetrics",
                dataSource: string.Empty);

            //var connector = new GenevaMetricsConnector
            //{
            //    MonitoringAccount = "ContainerAppsShoeboxNorthCentralUSStage",
            //    MetricNamespace = "k4apps-metrics"
            //};

            if (connector == null)
            {
                throw new InvalidOperationException("Geneva Metrics connector not found. Please ensure an GenevaMetrics connector is configured.");
            }

            return connector;
        }
    }
}
