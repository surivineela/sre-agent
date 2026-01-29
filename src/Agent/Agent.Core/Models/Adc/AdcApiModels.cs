// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models.Adc;

#region Disk Image Models

/// <summary>
/// Request to create a disk image.
/// </summary>
public record AdcCreateDiskImageRequest
{
    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; init; } = new();

    [JsonPropertyName("image")]
    public AdcDiskImageSpec Image { get; init; } = new();
}

/// <summary>
/// Disk image specification.
/// </summary>
public record AdcDiskImageSpec
{
    [JsonPropertyName("base")]
    public string Base { get; init; } = string.Empty;
}

/// <summary>
/// Response from disk image operations.
/// </summary>
public record AdcDiskImageResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("labels")]
    public Dictionary<string, string>? Labels { get; init; }

    [JsonPropertyName("status")]
    public AdcDiskImageStatus? Status { get; init; }
}

/// <summary>
/// Status of a disk image.
/// </summary>
public record AdcDiskImageStatus
{
    [JsonPropertyName("state")]
    public string State { get; init; } = string.Empty;

    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; init; }
}

/// <summary>
/// Simplified disk image info returned by IAdcApiClient.
/// </summary>
public record AdcDiskImageInfo
{
    public required string Id { get; init; }
    public required string State { get; init; }
    public string? ErrorMessage { get; init; }
}

#endregion

#region Sandbox Models

/// <summary>
/// Request to create a sandbox from a disk image.
/// </summary>
public record AdcCreateSandboxRequest
{
    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; init; } = new();

    [JsonPropertyName("sourcesRef")]
    public AdcSourcesRef SourcesRef { get; init; } = new();

    [JsonPropertyName("resources")]
    public AdcResourcesSpec? Resources { get; init; }
}

/// <summary>
/// Sources reference for sandbox creation.
/// </summary>
public record AdcSourcesRef
{
    [JsonPropertyName("diskImage")]
    public AdcDiskImageRef? DiskImage { get; init; }

    [JsonPropertyName("snapshot")]
    public AdcSnapshotRef? Snapshot { get; init; }
}

/// <summary>
/// Reference to a disk image.
/// </summary>
public record AdcDiskImageRef
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("isPublic")]
    public bool IsPublic { get; init; }
}

/// <summary>
/// Reference to a snapshot.
/// </summary>
public record AdcSnapshotRef
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}

/// <summary>
/// Resources specification for sandbox creation.
/// </summary>
public record AdcResourcesSpec
{
    [JsonPropertyName("cpu")]
    public string Cpu { get; init; } = "4";

    [JsonPropertyName("memory")]
    public string Memory { get; init; } = "8Gi";

    [JsonPropertyName("disk")]
    public string? Disk { get; init; }
}

/// <summary>
/// Response from sandbox operations.
/// </summary>
public record AdcSandboxResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("state")]
    public string? State { get; init; }

    [JsonPropertyName("ports")]
    public List<AdcPortInfo>? Ports { get; init; }
}

#endregion

#region Port Models

/// <summary>
/// Information about a configured port.
/// </summary>
public record AdcPortInfo
{
    [JsonPropertyName("port")]
    public int Port { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("activationMode")]
    public string? ActivationMode { get; init; }

    [JsonPropertyName("protocol")]
    public string? Protocol { get; init; }
}

/// <summary>
/// Request to update ports on a sandbox.
/// </summary>
public record AdcUpdatePortsRequest
{
    [JsonPropertyName("ports")]
    public List<AdcPortSpec> Ports { get; init; } = new();
}

/// <summary>
/// Response from update ports operation.
/// </summary>
public record AdcUpdatePortsResponse
{
    [JsonPropertyName("ports")]
    public List<AdcPortInfo> Ports { get; init; } = new();
}

/// <summary>
/// Specification for configuring a port.
/// </summary>
public record AdcPortSpec
{
    [JsonPropertyName("port")]
    public int Port { get; init; }

    [JsonPropertyName("name")]
    public string? Name { get; init; }

    [JsonPropertyName("protocol")]
    public string Protocol { get; init; } = "Http2";

    // ADC PUT API requires non-null URL, even though it's not used
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;
}

#endregion

#region Snapshot Models

/// <summary>
/// Request to create a snapshot.
/// </summary>
public record AdcCreateSnapshotRequest
{
    [JsonPropertyName("labels")]
    public Dictionary<string, string> Labels { get; init; } = new();
}

/// <summary>
/// Response from create snapshot operation.
/// </summary>
public record AdcCreateSnapshotResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;
}

/// <summary>
/// Response from get snapshot operation.
/// </summary>
public record AdcSnapshotResponse
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("sandboxId")]
    public string? SandboxId { get; init; }

    [JsonPropertyName("createdAtUtc")]
    public DateTime? CreatedAtUtc { get; init; }

    [JsonPropertyName("clusterId")]
    public string? ClusterId { get; init; }

    [JsonPropertyName("resources")]
    public AdcSnapshotResources? Resources { get; init; }
}

/// <summary>
/// Resources configuration in a snapshot.
/// </summary>
public record AdcSnapshotResources
{
    [JsonPropertyName("cpu")]
    public string? Cpu { get; init; }

    [JsonPropertyName("memory")]
    public string? Memory { get; init; }

    [JsonPropertyName("disk")]
    public string? Disk { get; init; }
}

#endregion
