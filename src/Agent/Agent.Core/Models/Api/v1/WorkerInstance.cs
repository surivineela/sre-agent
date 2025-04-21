// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Api.v1;

public record WorkerInstance
{
    public required string Id { get; init; }
    public required DateTimeOffset LastHeartbeat { get; init; }
    public required int CurrentAgentCount { get; set; }
    public required WorkerInstanceHealthState HealthState { get; set; }
}

public enum WorkerInstanceHealthState
{
    Ready,
    Initializing,
    Unhealthy
}
