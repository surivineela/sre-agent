// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Azure.Core;
using Azure.Identity;
using Azure.ResourceManager.AppService;
using Azure.ResourceManager;

namespace Agent.Plugins
{
    public class MetricsPlugin : IMetricsPlugin
    {
        private readonly ArmHelper _armHelper;
        public MetricsPlugin(ArmHelper armHelper)
        {
            _armHelper = armHelper;
        }
        public async Task<IReadOnlyList<CpuTimeSeriesData>> GetWebAppCpuMetrics(
            string resourceId)
        {
            Console.WriteLine($"[get_webapp_cpu_metrics] Invoked with resourceId: {resourceId}");

            var metrics = new List<Metric>
            {
                new Metric { Name = "CpuTime", Unit = "Seconds", Aggregation = "Total" },
            };

            // to get proper CPU value, you must divide by the number of cores within the VM machines that are running the process
            // this will give the min CPU utilization (since not all the cores may be running the process)
            var appServicePlanId = await _armHelper.GetAppServicePlanNameAsync(resourceId);
            var currentSku = await _armHelper.GetCurrentSkuAsync(appServicePlanId);
            int numCores = ArmHelper.GetNumberOfCoresFromSku(currentSku.Name);
            int numWorkers = await _armHelper.GetNumberOfWorkers(resourceId);

            var metricsData = await _armHelper.FetchMetricsAsync(
                resourceId.ToString(),
                metrics);

            return metricsData
                .Select(m => new CpuTimeSeriesData(
                    TimeStamp: m.Timestamp,
                    // m.Value is cpu time in seconds in a minute
                    AverageCpuUtilizationPercentage: (m.Value / 60 / numCores / numWorkers) * 100))
                .ToArray();
        }


        public async Task<IReadOnlyList<SuccessfulRequestVolumeTimeSeriesData>> GetSuccessfulRequestVolumeAsync(
            string resourceId)
        {
            Console.WriteLine($"[get_success_request_volume] Invoked with resourceId: {resourceId}");

            if (resourceId.EndsWith("pbatum-flex-eus2-demo2") || resourceId.Contains("fake-unhealthy"))
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

            var metricsData = await _armHelper.FetchMetricsAsync(
                resourceId.ToString(),
                metrics);

            return metricsData
                .Select(m => new SuccessfulRequestVolumeTimeSeriesData(
                    TimeStamp: m.Timestamp,
                    SuccessfulRequestCount: (int)m.Value))
                .ToArray();
        }

        public async Task<IReadOnlyList<RequestAvailabilitySeriesData>> GetFunctionAppRequestAvailability(
            string resourceId)
        {
            Console.WriteLine($"[get_functionapp_request_availability] Invoked with resourceId: {resourceId}");

            var metrics = new List<Metric>
            {
                new Metric { Name = "Requests", Unit = "count", Aggregation = "Total" },
                new Metric { Name = "Http5xx", Unit = "count", Aggregation = "Total" }
            };

            var metricsData = await _armHelper.FetchMetricsAsync(
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

        public async Task<IReadOnlyList<MemoryTimeSeriesData>> GetMemoryMetrics(
            string resourceId)
        {
            Console.WriteLine($"[get_webapp_and_functionapp_memory_metrics] Invoked with resourceId: {resourceId}");

            var metrics = new List<Metric>
            {
                new Metric { Name = "PrivateBytes", Unit = "Bytes", Aggregation = "Average" },
            };

            var metricsData = await _armHelper.FetchMetricsAsync(
                resourceId.ToString(),
                metrics);

            return metricsData
                .Select(m => new MemoryTimeSeriesData(
                    TimeStamp: m.Timestamp,
                    AverageMemoryInBytes: m.Value))
                .ToArray();
        }

        public async Task<IReadOnlyList<ThreadTimeSeriesData>> GetThreadMetrics(
            string resourceId)
        {
            try
            {
                Console.WriteLine($"[get_webapp_thread_metrics] Invoked with resourceId: {resourceId}");

                var metrics = new List<Metric>
                {
                    new Metric { Name = "Threads", Unit = "count", Aggregation = "Average" }
                };

                var metricsData = await _armHelper.FetchMetricsAsync(
                    resourceId,
                    metrics);

                return metricsData
                    .Select(m => new ThreadTimeSeriesData(
                        TimeStamp: m.Timestamp,
                        // m.Value is thread count
                        ThreadCount: m.Value))
                    .ToArray();
            }
            catch (Exception ex)
            {
                throw;
            }

        }
    }
}
