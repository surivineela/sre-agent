// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Configuration;

public class InstanceManagementSettings
{
    [Required]
    public int LeaderLeaseTTLSeconds { get; set; }

    [Required]
    public int LeaderLeaseTimerIntervalSeconds { get; set; }

    [Required]
    public int InstanceHeartbeatIntervalSeconds { get; set; }

    [Required]
    public int InstanceAssignmentTTLSeconds { get; set; }

    [Required]
    public int InstanceAssignmentWatchIntervalSeconds { get; set; }

    [Required]
    public int ReasoningLoopMaxRetryCount { get; set; }

    [Required]
    public bool ProcessingEnabled { get; set; }
}

