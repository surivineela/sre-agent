// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Mocks
{
    public class MockMetricsPlugin : IMetricsPlugin
    {
        private readonly TimeProvider _timeProvider;

        public List<string> UnhealthyResourceIds { get; } = new List<string>();

        public MockMetricsPlugin(TimeProvider timeProvider)
        {
            _timeProvider = timeProvider;
        }

        public Task<IReadOnlyList<RequestAvailabilitySeriesData>> GetFunctionAppRequestAvailability(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<MemoryTimeSeriesData>> GetMemoryMetrics(string resourceId)
        {
            throw new NotImplementedException();
        }

        public async Task<IReadOnlyList<SuccessfulRequestVolumeTimeSeriesData>> GetSuccessfulRequestVolumeAsync(string resourceId)
        {
            await Task.Yield();

            var now = _timeProvider.GetUtcNow();
            var start = new DateTime(now.Year, now.Month, now.Day, now.Hour, now.Minute, 0);

            var fakeResults = new List<SuccessfulRequestVolumeTimeSeriesData>
            {
                new SuccessfulRequestVolumeTimeSeriesData(start.AddSeconds(-90), 847),
                new SuccessfulRequestVolumeTimeSeriesData(start.AddSeconds(-60), 954),
                new SuccessfulRequestVolumeTimeSeriesData(start.AddSeconds(-30), 1025),
                
            };

            if (UnhealthyResourceIds.Contains(resourceId))
            {
                fakeResults.Add(new SuccessfulRequestVolumeTimeSeriesData(start.AddSeconds(0), 7));
            }
            else
            {
                fakeResults.Add(new SuccessfulRequestVolumeTimeSeriesData(start.AddSeconds(0), 978));
            }

            return fakeResults.AsReadOnly();
        }

        public Task<IReadOnlyList<CpuTimeSeriesData>> GetWebAppCpuMetrics(string resourceId)
        {
            throw new NotImplementedException();
        }

        public Task<IReadOnlyList<ThreadTimeSeriesData>> GetThreadMetrics(string resourceId)
        {
            throw new NotImplementedException();
        }
    }
}
