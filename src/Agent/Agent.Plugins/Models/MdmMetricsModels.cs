// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Globalization;
using System.Text.Json.Serialization;

namespace Agent.Plugins;

internal sealed record DimensionFilterDescriptor(string Name, IReadOnlyList<string> Values);

internal sealed class MultipleTimeSeriesRequest
{
    [JsonPropertyName("startTimeUtc")]
    public DateTime StartTimeUtc { get; set; }

    [JsonPropertyName("endTimeUtc")]
    public DateTime EndTimeUtc { get; set; }

    [JsonPropertyName("samplingTypes")]
    public string[]? SamplingTypes { get; set; }

    [JsonPropertyName("seriesResolutionInMinutes")]
    public int SeriesResolutionInMinutes { get; set; } = 1;

    [JsonPropertyName("aggregationType")]
    public string? AggregationType { get; set; }

    [JsonPropertyName("definitions")]
    public List<MetricDefinitionPayload> Definitions { get; set; } = new();
}

internal sealed class MetricDefinitionPayload
{
    [JsonPropertyName("monitoringAccount")]
    public string? MonitoringAccount { get; set; }

    [JsonPropertyName("metricNamespace")]
    public string? MetricNamespace { get; set; }

    [JsonPropertyName("metricName")]
    public string? MetricName { get; set; }
}

internal sealed class TimeSeriesRequest
{
    [JsonPropertyName("monitoringAccount")]
    public string? MonitoringAccount { get; set; }

    [JsonPropertyName("metricNamespace")]
    public string? MetricNamespace { get; set; }

    [JsonPropertyName("metricName")]
    public string? MetricName { get; set; }

    [JsonPropertyName("startTimeUtc")]
    public DateTime StartTimeUtc { get; set; }

    [JsonPropertyName("endTimeUtc")]
    public DateTime EndTimeUtc { get; set; }

    [JsonPropertyName("samplingTypes")]
    public string[]? SamplingTypes { get; set; }

    [JsonPropertyName("seriesResolutionInMinutes")]
    public int SeriesResolutionInMinutes { get; set; } = 1;

    [JsonPropertyName("aggregationType")]
    public string? AggregationType { get; set; }

    [JsonPropertyName("dimensionFilters")]
    public List<DimensionFilterPayload>? DimensionFilters { get; set; }

    [JsonPropertyName("outputDimensionNames")]
    public string[]? OutputDimensionNames { get; set; }

    [JsonPropertyName("lastValueMode")]
    public bool? LastValueMode { get; set; }

    public void Normalize()
    {
        StartTimeUtc = NormalizeToMinute(StartTimeUtc);
        EndTimeUtc = NormalizeToMinute(EndTimeUtc);
        if (EndTimeUtc < StartTimeUtc)
        {
            throw new ArgumentException("End time must be greater than or equal to start time.");
        }
    }

    private static DateTime NormalizeToMinute(DateTime utc) =>
        new(DateTime.SpecifyKind(utc, DateTimeKind.Utc).Ticks / TimeSpan.TicksPerMinute * TimeSpan.TicksPerMinute, DateTimeKind.Utc);
}

internal sealed class DimensionFilterPayload
{
    [JsonPropertyName("dimension")]
    public string? Dimension { get; set; }

    [JsonPropertyName("name")]
    public string? Name { get; set; }

    [JsonPropertyName("values")]
    public List<string>? Values { get; set; }
}
