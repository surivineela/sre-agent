// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Implementation.AzureApplicationInsightsPlugin.Models.Query;

public class ImpactQueryResponse
{
    public string? cloud_RoleName { get; set; }
    public long ImpactedInstances { get; set; } = 0;

    public long TotalInstances { get; set; } = 0;

    public long ImpactedRequests { get; set; } = 0;

    public long TotalRequests { get; set; } = 0;

    public double ImpactedRequestsPercent { get; set; } = 0.0;

    public double ImpactedInstancePercent { get; set; } = 0.0;
}
