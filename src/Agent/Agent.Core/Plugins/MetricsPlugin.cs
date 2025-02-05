// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Core.Models;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace Agent.Core.Plugins;

public class MetricsPlugin
{

    [KernelFunction("get_webapp_cpu_metrics")]
    [Description("Get the average CPU utilization metrics of a specific WebApp instance at per minute granularity" +
        " for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% CPU utilization, zero metric value doesn't indicate the app is unhealthy")]
    public async Task<IReadOnlyList<CpuTimeSeriesData>> GetWebAppCpuMetrics(
        [Description("The resource ID of the WebApp resource.")]
            string resourceId)
    {
        Console.WriteLine($"[get_webapp_cpu_metrics] Invoked with resourceId: {resourceId}");

        var metrics = new List<Metric>
            {
                new Metric { Name = "CpuTime", Unit = "Seconds", Aggregation = "Total" },
                // new Metric { Name = "MemoryWorkingSet", Unit = "", Aggregation = "Average" }
            };

        var metricsData = await ArmHelper.FetchMetricsAsync(
            resourceId.ToString(),
            metrics);

        return metricsData
            .Select(m => new CpuTimeSeriesData(
                TimeStamp: m.Timestamp,
                // m.Value is cpu time in seconds in a minute
                AverageCpuUtilizationPercentage: (m.Value / 60) * 100))
            .ToArray();
    }


    [KernelFunction("get_success_request_volume")]
    [Description("Get the 2XX request volume of a specific resource at per minute granularity")]
    public async Task<IReadOnlyList<SuccessfulRequestVolumeTimeSeriesData>> GetSuccessfulRequestVolumeAsync(
        [Description("The resource ID of the WebApp resource.")]
            string resourceId)
    {
        Console.WriteLine($"[get_success_request_volume] Invoked with resourceId: {resourceId}");

        if (resourceId.EndsWith("pbatum-flex-eus2-demo2"))
        {
            // demo fakery mode activated
            var now = DateTime.UtcNow;
            var start = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);
            var fakeResults = new List<SuccessfulRequestVolumeTimeSeriesData>
                {
                    new SuccessfulRequestVolumeTimeSeriesData(start.AddSeconds(-90), 847),
                    new SuccessfulRequestVolumeTimeSeriesData(start.AddSeconds(-60), 954),
                    new SuccessfulRequestVolumeTimeSeriesData(start.AddSeconds(-30), 1025),
                    new SuccessfulRequestVolumeTimeSeriesData(start.AddSeconds(0), 7),
                };
            return fakeResults.ToArray();
        }

        var metrics = new List<Metric>
            {
                new Metric { Name = "Http2xx", Unit = "Count", Aggregation = "Total" },
            };

        var metricsData = await ArmHelper.FetchMetricsAsync(
            resourceId.ToString(),
            metrics);

        return metricsData
            .Select(m => new SuccessfulRequestVolumeTimeSeriesData(
                TimeStamp: m.Timestamp,
                SuccessfulRequestCount: (int)m.Value))
            .ToArray();
    }

    [KernelFunction("get_functionapp_request_availability")]
    [Description("Get the request availability of a specific FunctionApp at per minute granularity" +
        " for the past 30 minutes, FunctionApp is healthy if all data points are at least 99.9 availability")]
    public async Task<IReadOnlyList<RequestAvailabilitySeriesData>> GetFunctionAppRequestAvailability(
        [Description("The resource ID of the FunctionApp resource.")]
            string resourceId)
    {
        Console.WriteLine($"[get_functionapp_request_availability] Invoked with resourceId: {resourceId}");

        var metrics = new List<Metric>
            {
                new Metric { Name = "Requests", Unit = "count", Aggregation = "Total" },
                new Metric { Name = "Http5xx", Unit = "count", Aggregation = "Total" }
            };

        var metricsData = await ArmHelper.FetchMetricsAsync(
            resourceId.ToString(),
            metrics);


        var requestsTimeSeries = metricsData.Where(m => m.Unit == "count" && m.Name == "Requests").ToList();
        var http5xxTimeSeries = metricsData.Where(m => m.Unit == "count" && m.Name == "Http5xx").ToList();

        var availabilityData = new List<RequestAvailabilitySeriesData>();
        foreach (var request in requestsTimeSeries)
        {
            var timestamp = request.Timestamp;
            var totalRequests = request.Value;

            var http5xx = http5xxTimeSeries.FirstOrDefault(h => h.Timestamp == timestamp)?.Value ?? 0;
            var availability = totalRequests == 0 ? 100.0 : (totalRequests - http5xx) / totalRequests * 100;

            availabilityData.Add(new RequestAvailabilitySeriesData(
                TimeStamp: timestamp,
                AvailabilityPercentage: availability));
        }
        return availabilityData;
    }

    [KernelFunction("get_webapp_and_functionapp_memory_metrics")]
    [Description("Get the average memory utilization metrics of a specific WebApp or FunctionApp instance at per minute granularity" +
        " for the past 30 minutes, WebApp is healthy if over half of the data points is less than 80% memory utilization.")]
    public async Task<IReadOnlyList<MemoryTimeSeriesData>> GetMemoryMetrics(
        [Description("The resource ID of the WebApp or FunctionApp resource.")]
            string resourceId)
    {
        Console.WriteLine($"[get_webapp_and_functionapp_memory_metrics] Invoked with resourceId: {resourceId}");

        var metrics = new List<Metric>
            {
                new Metric { Name = "MemoryWorkingSet", Unit = "Bytes", Aggregation = "Average" },
            };

        var metricsData = await ArmHelper.FetchMetricsAsync(
            resourceId.ToString(),
            metrics);

        return metricsData
            .Select(m => new MemoryTimeSeriesData(
                TimeStamp: m.Timestamp,
                AverageMemoryInBytes: m.Value))
            .ToArray();
    }
}


public sealed record CpuTimeSeriesData(
    DateTime TimeStamp,
    double AverageCpuUtilizationPercentage);

public sealed record MemoryTimeSeriesData(
    DateTime TimeStamp,
    double AverageMemoryInBytes);

public sealed record RequestAvailabilitySeriesData(
    DateTime TimeStamp,
    double AvailabilityPercentage);

public sealed record SuccessfulRequestVolumeTimeSeriesData(
    DateTime TimeStamp,
    int SuccessfulRequestCount);
