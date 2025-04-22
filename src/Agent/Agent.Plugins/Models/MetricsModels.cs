// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins
{
    public sealed record CpuTimeSeriesData(
        DateTime TimeStamp,
        double AverageCpuUtilizationPercentage);

    public sealed record SuccessfulRequestVolumeTimeSeriesData(
        DateTime TimeStamp,
        int SuccessfulRequestCount);

    public sealed record RequestAvailabilitySeriesData(
        DateTime TimeStamp,
        double AvailabilityPercentage);

    public sealed record MemoryTimeSeriesData(
        DateTime TimeStamp,
        double AverageMemoryInBytes);

    public sealed record ThreadTimeSeriesData(
        DateTime TimeStamp,
        double ThreadCount);

    public sealed record LatencySeriesData(
        DateTime TimeStamp,
        double AverageLatencyInMilliseconds);

}
