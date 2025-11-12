namespace Agent.Tests.Unit;

using System;
using System.Linq;
using Agent.Prometheus;
using Agent.Prometheus.Extensions;
using Kusto.Cloud.Platform.Utils;
using Xunit;

public class PrometheusParserTests
{
    [Fact]
    public void TestParse_ValidExpositionText()
    {
        string expositionText = @"
# HELP http_requests_total The total number of HTTP requests.
# TYPE http_requests_total counter
http_requests_total{method=""post"",code=""200""} 1027
http_requests_total{method=""post"",code=""400""} 3
# HELP memory_usage_bytes Memory usage in bytes.
# TYPE memory_usage_bytes gauge
memory_usage_bytes{host=""server1""} 12345678
memory_usage_bytes{host=""server2""} 87654321
";

        var metricFamilies = PrometheusTextParser.Parse(expositionText);

        Assert.Equal(2, metricFamilies.Count);

        // Test first metric family
        var httpRequests = metricFamilies.FirstOrDefault(mf => mf.Name == "http_requests_total");
        Assert.NotNull(httpRequests);
        Assert.Equal("The total number of HTTP requests.", httpRequests.Help);
        Assert.Equal("counter", httpRequests.Type);
        Assert.Equal(2, httpRequests.Metrics.Count);

        var metric1 = httpRequests.Metrics.FirstOrDefault(m => m.Name == "http_requests_total" && m.Labels["method"] == "post" && m.Labels["code"] == "200");
        Assert.NotNull(metric1);
        Assert.Equal(1027, metric1.Value);

        var metric2 = httpRequests.Metrics.FirstOrDefault(m => m.Name == "http_requests_total" && m.Labels["method"] == "post" && m.Labels["code"] == "400");
        Assert.NotNull(metric2);
        Assert.Equal(3, metric2.Value);

        // Test second metric family
        var memoryUsage = metricFamilies.FirstOrDefault(mf => mf.Name == "memory_usage_bytes");
        Assert.NotNull(memoryUsage);
        Assert.Equal("Memory usage in bytes.", memoryUsage.Help);
        Assert.Equal("gauge", memoryUsage.Type);
        Assert.Equal(2, memoryUsage.Metrics.Count);

        var memoryMetric1 = memoryUsage.Metrics.FirstOrDefault(m => m.Labels["host"] == "server1");
        Assert.NotNull(memoryMetric1);
        Assert.Equal(12345678, memoryMetric1.Value);

        var memoryMetric2 = memoryUsage.Metrics.FirstOrDefault(m => m.Labels["host"] == "server2");
        Assert.NotNull(memoryMetric2);
        Assert.Equal(87654321, memoryMetric2.Value);
    }

    [Fact]
    public void TestParse_EmptyExpositionText()
    {
        string expositionText = "";

        var metricFamilies = PrometheusTextParser.Parse(expositionText);

        Assert.Empty(metricFamilies);
    }

    [Fact]
    public void TestParse_InvalidExpositionText()
    {
        string expositionText = @"
# HELP invalid_metric This is an invalid metric.
invalid_metric{label_without_value} 123
";

        Assert.Throws<FormatException>(() => PrometheusTextParser.Parse(expositionText)).Message.Contains("Invalid label format: label_without_value");
    }

    [Fact]
    public void TestParse_RealData()
    {
        var testDataPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "TestData", "PrometheusTextFormat.txt");
        string realData = File.ReadAllText(testDataPath);
        var metricFamilies = PrometheusTextParser.Parse(realData);
        Assert.NotEmpty(metricFamilies);
        var metricFamily = metricFamilies.Find(mf => mf.Name == "microsoftidentityclient_common_meter_msalfailure");
        Assert.NotNull(metricFamily);
        Assert.NotEmpty(metricFamily.Metrics);
        // Test whether a label with ',' in its value is parsed correctly
        Assert.Equal(",", metricFamily.Metrics[0].Labels["callersdkid"]);
        Assert.Equal("AcquireTokenForSystemAssignedManagedIdentity", metricFamily.Metrics[0].Labels["apiid"]);
        Assert.Equal(13, metricFamily.Metrics[0].Value);

        metricFamily = metricFamilies.Find(mf => mf.Name == "gremlin_query_latency_seconds");
        Assert.NotNull(metricFamily);
        Assert.NotEmpty(metricFamily.Metrics);
        Assert.Equal("gauge", metricFamily.Type);
        Assert.Equal("Latency of Gremlin queries in seconds", metricFamily.Help);
        var countMetric = metricFamily.Metrics.Where(m => m.Labels["query_type"] == "count").ToList();
        Assert.Single(countMetric);
        Assert.Equal(0.1739456, countMetric[0].Value, precision: 6);

        var groupCountMetric = metricFamily.Metrics.Where(m => m.Labels["query_type"] == "group_count").ToList();
        Assert.Single(groupCountMetric);
        Assert.Equal(0.1799265, groupCountMetric[0].Value, precision: 6);

        var customMetric = metricFamily.Metrics.Where(m => m.Labels["query_type"] == "custom").ToList();
        Assert.Single(customMetric);
        Assert.Equal(0.1736283, customMetric[0].Value, precision: 6);

        var dedupMetric = metricFamily.Metrics.Where(m => m.Labels["query_type"] == "dedup").ToList();
        Assert.Single(dedupMetric);
        Assert.Equal(0.206548, dedupMetric[0].Value, precision: 6);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        var writeRequest = metricFamilies.ToWriteRequest(timestamp);
        Assert.NotNull(writeRequest);
        Assert.NotEmpty(writeRequest.Timeseries);
        Assert.True(writeRequest.Timeseries.Count > 0);
        Assert.True(writeRequest.Metadata.Count > 0);
        Assert.Contains(writeRequest.Metadata, m => m.MetricFamilyName == "microsoftidentityclient_common_meter_msalfailure");
        var mf = writeRequest.Metadata.First(m => m.MetricFamilyName == "microsoftidentityclient_common_meter_msalfailure");
        Assert.Equal(global::Prometheus.Protobuf.MetricMetadata.Types.MetricType.Gauge, mf.Type);
        Assert.Equal("Number of failed token acquisition calls (Counter`1)", mf.Help);
        var ts = writeRequest.Timeseries.Where(ts => ts.Labels.Any(l => l.Name == "__name__" && l.Value == "microsoftidentityclient_common_meter_msalfailure")).ToList();
        Assert.Single(ts);
        Assert.Single(ts.First().Samples);
        Assert.Equal(13, ts.First().Samples[0].Value, precision: 6);
        Assert.Contains(ts.First().Labels, l => l.Name == "callersdkid" && l.Value == ",");
        Assert.Contains(ts.First().Labels, l => l.Name == "apiid" && l.Value == "AcquireTokenForSystemAssignedManagedIdentity");
        Assert.Contains(ts.First().Labels, l => l.Name == "__name__" && l.Value == "microsoftidentityclient_common_meter_msalfailure");

        Assert.Contains(writeRequest.Metadata, m => m.MetricFamilyName == "gremlin_query_latency_seconds");
        mf = writeRequest.Metadata.First(m => m.MetricFamilyName == "gremlin_query_latency_seconds");
        Assert.Equal(global::Prometheus.Protobuf.MetricMetadata.Types.MetricType.Gauge, mf.Type);
        Assert.Equal("Latency of Gremlin queries in seconds", mf.Help);
        ts = writeRequest.Timeseries.Where(ts => ts.Labels.Any(l => l.Name == "__name__" && l.Value == "gremlin_query_latency_seconds")).ToList();
        Assert.Equal(4, ts.Count);
        Assert.Equal(0.1739456, ts[0].Samples[0].Value, precision: 6);
        Assert.Equal(0.1799265, ts[1].Samples[0].Value, precision: 6);
        Assert.Equal(0.1736283, ts[2].Samples[0].Value, precision: 6);
        Assert.Equal(0.206548, ts[3].Samples[0].Value, precision: 6);
        Assert.Contains(ts[0].Labels, l => l.Name == "query_type" && l.Value == "count");
        Assert.Contains(ts[1].Labels, l => l.Name == "query_type" && l.Value == "group_count");
        Assert.Contains(ts[2].Labels, l => l.Name == "query_type" && l.Value == "custom");
        Assert.Contains(ts[3].Labels, l => l.Name == "query_type" && l.Value == "dedup");
        writeRequest.Timeseries.ForEach(ts =>
        {
            Assert.NotEmpty(ts.Labels);
            Assert.NotEmpty(ts.Samples);
            Assert.NotEmpty(ts.Labels);

            // Must contain a label with the name "__name__"
            Assert.Equal(1, ts.Labels.Count(label => label.Name == "__name__"));

            ts.Samples.ForEach(sample =>
            {
                Assert.Equal(timestamp, sample.Timestamp);
            });
        });
    }
}
