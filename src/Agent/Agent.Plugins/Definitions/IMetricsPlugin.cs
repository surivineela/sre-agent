// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins
{
    public interface IMetricsPlugin
    {
        Task<IReadOnlyList<CpuTimeSeriesData>> GetWebAppCpuMetrics(string resourceId);
        Task<IReadOnlyList<SuccessfulRequestVolumeTimeSeriesData>> GetSuccessfulRequestVolumeAsync(string resourceId);
        Task<IReadOnlyList<RequestAvailabilitySeriesData>> GetFunctionAppRequestAvailability(string resourceId);
        Task<IReadOnlyList<MemoryTimeSeriesData>> GetMemoryMetrics(string resourceId);
        Task<IReadOnlyList<RequestCountTimeSeriesData>> GetContainerAppRequestsMetrics(string resourceId);
    }
}