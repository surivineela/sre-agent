
// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Prometheus.Extensions;

public static class MetricFamiliesExtensions
{
    // The implementation loosely follows the Prometheus Go implementation that converts metrics famlilies to a WriteRequest.
    // https://github.com/prometheus/prometheus/blob/v3.2.1/util/fmtutil/format.go
    // A sample of how a write request looks like: https://github.com/prometheus/prometheus/blob/v3.2.1/util/fmtutil/format_test.go
    public static global::Prometheus.Protobuf.WriteRequest ToWriteRequest(this List<MetricFamily> metricFamilies, long timestamp)
    {
        global::Prometheus.Protobuf.WriteRequest request = new();

        // Sort the metric families in lexicographical order
        metricFamilies.Sort((x, y) => string.Compare(x.Name, y.Name, StringComparison.OrdinalIgnoreCase));

        foreach (var metricFamily in metricFamilies)
        {
            // A metric family is a collection of metrics with the same name but different labels.
            if (!Enum.TryParse(metricFamily.Type, ignoreCase: true, out global::Prometheus.Protobuf.MetricMetadata.Types.MetricType metricType))
            {
                throw new ArgumentException($"Invalid metric type: {metricFamily.Type}");
            }

            var metadata = new global::Prometheus.Protobuf.MetricMetadata
            {
                MetricFamilyName = metricFamily.Name,
                Type = metricType,
                Help = metricFamily.Help
            };
            request.Metadata.Add(metadata);

            foreach (var metric in metricFamily.Metrics)
            {
                var timeSeries = new global::Prometheus.Protobuf.TimeSeries()
                {
                    Labels = { new global::Prometheus.Protobuf.Label { Name = "__name__", Value = metric.Name } }
                };
                foreach (var label in metric.Labels)
                {
                    timeSeries.Labels.Add(new global::Prometheus.Protobuf.Label { Name = label.Key, Value = label.Value });
                }

                var sample = new global::Prometheus.Protobuf.Sample
                {
                    Value = metric.Value,
                    Timestamp = timestamp
                };
                timeSeries.Samples.Add(sample);
                request.Timeseries.Add(timeSeries);
            }
        }

        return request;
    }
}
