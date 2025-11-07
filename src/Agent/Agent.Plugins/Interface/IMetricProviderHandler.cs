// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Models;

namespace Agent.Plugins.Interface
{
    /// <summary>
    /// Handler interface for fetching metrics from different providers
    /// </summary>
    public interface IMetricProviderHandler
    {
        /// <summary>
        /// Gets the name of the metric provider this handler supports
        /// </summary>
        string ProviderName { get; }

        /// <summary>
        /// Discovers and returns all available metric names from the provider
        /// </summary>
        /// <returns>Array of metric names available in this provider</returns>
        Task<MetricDefinition[]> DiscoverMetricsAsync(string? resourceId = null);

        /// <summary>
        /// Gets the dimensions for a specific metric. This helps identify what dimensions are available for filtering.
        /// </summary>
        /// <param name="metricName">Name of the metric to get dimensions for</param>
        /// <returns>Array of dimension names available for the specified metric</returns>
        Task<string[]> GetDimensionNamesAsync(string metricName, string? resourceId = null);

        /// <summary>
        /// Fetches time series data from the metric provider
        /// </summary>
        /// <param name="metricName">Name of the metric to query</param>
        /// <param name="startTime">Start time for the query</param>
        /// <param name="endTime">End time for the query</param>
        /// <param name="filter">Dictionary of dimension filters</param>
        /// <param name="aggregation">Aggregation type to apply</param>
        /// <returns>Array of time series data</returns>
        Task<TimeSeries[]> FetchMetricDataAsync(
            string metricName,
            DateTime startTime,
            DateTime endTime,
            DimensionFilter[] filters,
            string aggregation,
            string? resourceId = null);
    }
}
