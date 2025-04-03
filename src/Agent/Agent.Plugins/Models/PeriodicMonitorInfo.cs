// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Models
{
    public sealed record PeriodicMonitorInfo(
        string ResourceId,
        TimeSpan MonitorInterval,
        bool? LastCheckWasHealthy,
        DateTime? LastExecution);
}

