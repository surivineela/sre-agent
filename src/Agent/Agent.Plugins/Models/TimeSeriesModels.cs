// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace Agent.Plugins.Models
{
    /// <summary>
    /// Time series data point with double value
    /// </summary>
    public sealed record TimeSeriesDataPoint(
        DateTime Timestamp,
        double Value);

    /// <summary>
    /// Time series collection for a single metric
    /// </summary>
    public sealed record TimeSeries(
        string MetricName,
        string Unit,
        AggregationType Aggregation,
        IDictionary<string, string> Dimensions,
        IReadOnlyList<TimeSeriesDataPoint> DataPoints);

    /// <summary>
    /// Aggregation methods for resampling
    /// </summary>
    public enum AggregationType
    {
        Average,
        Sum,
        Min,
        Max,
        Count
    }

    /// <summary>
    /// Generic metric definition
    /// </summary>
    public sealed record MetricDefinition(
        string Name,
        string Description);

    public sealed record DimensionFilter(
        [Description("The name of the dimension to filter on (e.g. region).")] string Dimension,
        [Description("The value of the dimension to filter on (e.g. eastus)")] string Value);

    public static class TimeSeriesExtensions
    {
        public static string FormatDimensions(this TimeSeries series)
        {
            return string.Join("\n", series.Dimensions.Select(kv => kv.Value));
        }
    }
}
