// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Prometheus;

namespace Agent.Prometheus.Extensions;

public static class GaugeExtensions
{
    public static global::Prometheus.Protobuf.WriteRequest ToWriteRequest(this global::Prometheus.Gauge gauge, double val, params string[] labelValues)
    {
        var labelNames = gauge.LabelNames;

        if (labelNames.Length != labelValues.Length)
        {
            throw new ArgumentException($"Label names and values count mismatch. Names: {gauge.LabelNames}, Values: {labelValues}");
        }

        global::Prometheus.Protobuf.TimeSeries timeSeries = new()
        {
            Labels = { new global::Prometheus.Protobuf.Label { Name = "__name__", Value = gauge.Name } }
        };

        for (int i = 0; i < labelNames.Length; i++)
        {
            timeSeries.Labels.Add(new global::Prometheus.Protobuf.Label { Name = labelNames[i], Value = labelValues[i] });
        }

        timeSeries.Samples.Add(new global::Prometheus.Protobuf.Sample { Value = val, Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() });

        global::Prometheus.Protobuf.WriteRequest request = new();
        request.Timeseries.Add(timeSeries);
        return request;
    }
}