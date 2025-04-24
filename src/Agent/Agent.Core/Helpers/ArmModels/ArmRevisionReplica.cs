// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Helpers.ArmModels;

public class ArmRevisionReplica
{
    [JsonPropertyName("createdTime")]
    public string? CreatedTime { get; init; }

    [JsonPropertyName("runningState")]
    public string? RunningState { get; init; }

    [JsonPropertyName("runningStateDetails")]
    public string? RunningStateDetails { get; init; }

    [JsonPropertyName("containers")]
    public IReadOnlyCollection<ArmRevisionReplicaContainer> Containers { get; init; } = [];

    [JsonPropertyName("initContainers")]
    public IReadOnlyCollection<ArmRevisionReplicaContainer> InitContainers { get; init; } = [];
}

public class ArmRevisionReplicaContainer
{
    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("containerId")]
    public string? ContainerId { get; init; }

    [JsonPropertyName("ready")]
    public bool Ready { get; init; }

    [JsonPropertyName("restartCount")]
    public int RestartCount { get; init; }

    [JsonPropertyName("runningState")]
    public string? RunningState { get; init; }

    [JsonPropertyName("runningStateDetails")]
    public string? RunningStateDetails { get; init; }

    [JsonPropertyName("logStreamEndpoint")]
    public string? LogStreamEndpoint { get; init; }

    [JsonPropertyName("execEndpoint")]
    public string? ExecEndpoint { get; init; }
}
