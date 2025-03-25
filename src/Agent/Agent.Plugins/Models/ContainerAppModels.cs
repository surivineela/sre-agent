// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Models
{
    public sealed record ContainerAppDescriptor(
        string ResourceId,
        string Name,
        string Kind,
        string Location,
        string WorkloadProfile,
        string State,
        string ResourceGroup,
        string Fqdn,
        string EnvironmentName = "N/A",
        bool IsIngressEnabled = false,
        IReadOnlyList<RevisionInfo> Revisions = null);
    
    public sealed record RevisionInfo(
        string RevisionName,
        bool IsActive,
        int TrafficWeight);

    public sealed record RequestCountTimeSeriesData(
        DateTime TimeStamp,
        double TotalRequestCount);

    public sealed record CpuUsageTimeSeriesData(
        DateTime TimeStamp,
        double Percent);

    public sealed record MemoryUsageTimeSeriesData(
        DateTime TimeStamp,
        double Percent);
}
