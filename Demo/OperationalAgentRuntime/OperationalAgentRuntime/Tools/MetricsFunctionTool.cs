using System.ComponentModel;
using System.Diagnostics;
using System.Text.Json;
using Microsoft.SemanticKernel;
using OperationalAgentRuntime.Helpers;
using OperationalAgentRuntime.Models;

namespace OperationalAgentRuntime.Tools
{
    internal class MetricsFunctionTool
    {
        [KernelFunction, Description("Get the availability of an app.")]
        public async Task<List<TimeSeriesData>> GetAppAvailability(
            [Description("The resource ID of the app.")]
            string appResourceId
            )
        {
            var metrics = new List<Metric>
        {
            new Metric { Name = "Requests", Unit = "count", Aggregation = "Total" },
            new Metric { Name = "Http5xx", Unit = "count", Aggregation = "Total" }
            };

            var metricsData = await ArmHelper.FetchMetricsAsync(appResourceId, metrics);

            var requestsTimeSeries = metricsData.Where(m => m.Unit == "count" && m.Name == "Requests").ToList();
            var http5xxTimeSeries = metricsData.Where(m => m.Unit == "count" && m.Name == "Http5xx").ToList();

            var availabilityData = new List<TimeSeriesData>();
            foreach (var request in requestsTimeSeries)
            {
                var timestamp = request.Timestamp;
                var totalRequests = request.Value;

                var http5xx = http5xxTimeSeries.FirstOrDefault(h => h.Timestamp == timestamp)?.Value ?? 0;
                var availability = totalRequests == 0 ? 100.0 : (totalRequests - http5xx) / totalRequests * 100;

                availabilityData.Add(new TimeSeriesData
                {
                    Timestamp = timestamp,
                    Value = availability,
                    Unit = "percent"
                });
            }

            Debug.WriteLine(JsonSerializer.Serialize(availabilityData));

            return availabilityData;
        }

            [KernelFunction, Description(
"""
Get the specified metric for an app.

| Metric Name                    | Name in REST API    | Unit      | Aggregation    | Dimensions | Time Grains |
|--------------------------------|---------------------|-----------|----------------|------------|-------------|
| **CPU Time**                   | CpuTime             | Seconds   | Count, Sum     | Instance   | PT1M        |
| **Memory Working Set**         | MemoryWorkingSet    | Bytes     | Average        | Instance   | PT1M        |
| **Http 2xx**                   | Http2xx             | Count     | Total (Sum)    | Instance   | PT1M        |
| **Http 5xx**                   | Http5xx             | Count     | Total (Sum)    | Instance   | PT1M        |
| **Requests**                   | Requests            | Count     | Total (Sum)    | Instance   | PT1M        |
| **Http Response Time**         | HttpResponseTime    | Seconds   | Average        | Instance   | PT1M        |
""")]
        public async Task<List<TimeSeriesData>> GetMetricAsync(
            [Description("The resource ID of the app.")]
            string appResourceId,
            [Description("The name of the metric. ")]
            string metricName,
            [Description("The unit of the metric.")]
            string unit,
            [Description("The aggregation type of the metric.")]
            string aggregation
            )        
        {
            var metrics = new List<Metric>
            {
                new Metric { Aggregation = aggregation, Name = metricName, Unit = unit }
            };

            var metricsData = await ArmHelper.FetchMetricsAsync(appResourceId, metrics);

            Debug.WriteLine(JsonSerializer.Serialize(metricsData));
            return metricsData;
        }
    }
}
