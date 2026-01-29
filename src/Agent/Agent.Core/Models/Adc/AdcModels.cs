// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Core.Models.Adc;

/// <summary>
/// Represents ADC resource allocation.
/// </summary>
public record AdcResources
{
    /// <summary>
    /// CPU allocation (e.g., "4").
    /// </summary>
    public string Cpu { get; init; } = "4";

    /// <summary>
    /// Memory allocation (e.g., "8Gi").
    /// </summary>
    public string Memory { get; init; } = "8Gi";

    /// <summary>
    /// Disk size (e.g., "50Gi").
    /// </summary>
    public string Disk { get; init; } = "50Gi";
}

/// <summary>
/// Represents per-thread ADC sandbox state.
/// </summary>
public record AdcSandboxInfo
{
    /// <summary>
    /// ADC sandbox identifier returned from API.
    /// </summary>
    public required string SandboxId { get; init; }

    /// <summary>
    /// Owning thread ID. One sandbox per thread.
    /// </summary>
    public required string ThreadId { get; init; }

    /// <summary>
    /// Primary endpoint URL for the sandbox.
    /// </summary>
    public required string Endpoint { get; init; }

    /// <summary>
    /// Snapshot ID this sandbox was created/restored from (null if from disk image).
    /// </summary>
    public string? SnapshotId { get; init; }

    /// <summary>
    /// Allocated resources (cpu, memory, disk).
    /// </summary>
    public required AdcResources Resources { get; init; }

    /// <summary>
    /// Sandbox creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Represents the ADC base snapshot configuration.
/// </summary>
public record AdcBaseSnapshotInfo
{
    /// <summary>
    /// Current ADC base snapshot identifier.
    /// </summary>
    public required string SnapshotId { get; init; }

    /// <summary>
    /// Source disk image ID this snapshot was created from.
    /// </summary>
    public required string DiskImageId { get; init; }

    /// <summary>
    /// Docker base image reference (for change detection).
    /// </summary>
    public required string BaseImage { get; init; }

    /// <summary>
    /// Snapshot name (format: basesnapshot-{timestamp}).
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Resource configuration of the snapshot.
    /// </summary>
    public required AdcResources Resources { get; init; }

    /// <summary>
    /// Snapshot creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; init; }
}

/// <summary>
/// Represents a snapshot created from a thread sandbox.
/// </summary>
public record AdcSnapshotInfo
{
    /// <summary>
    /// ADC snapshot identifier.
    /// </summary>
    public required string SnapshotId { get; init; }

    /// <summary>
    /// Owning thread ID.
    /// </summary>
    public required string ThreadId { get; init; }

    /// <summary>
    /// User-friendly name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// The global base snapshot ID this thread snapshot originated from.
    /// </summary>
    public required string OriginalBaseSnapshotId { get; init; }

    /// <summary>
    /// Snapshot creation timestamp.
    /// </summary>
    public DateTime CreatedAt { get; init; }
}
