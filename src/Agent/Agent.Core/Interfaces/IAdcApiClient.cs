// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Adc;

namespace Agent.Core.Interfaces;

/// <summary>
/// Low-level client for ADC (Azure Dev Compute) REST API operations.
/// This is a pure API client with no persistence or business logic.
/// Models used by this interface are defined in <see cref="Agent.Core.Models.Adc"/>.
/// </summary>
public interface IAdcApiClient
{
    #region Disk Images

    /// <summary>
    /// Creates a new disk image from the specified base image.
    /// </summary>
    /// <param name="baseImage">The base image reference (e.g., Docker image).</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The disk image ID.</returns>
    Task<string> CreateDiskImageAsync(string baseImage, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of a disk image.
    /// </summary>
    /// <param name="diskImageId">The disk image ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Disk image info including state.</returns>
    Task<AdcDiskImageInfo> GetDiskImageAsync(string diskImageId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for a disk image to reach the Ready state.
    /// </summary>
    /// <param name="diskImageId">The disk image ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WaitForDiskImageReadyAsync(string diskImageId, CancellationToken cancellationToken = default);

    #endregion

    #region Sandboxes

    /// <summary>
    /// Creates a new sandbox from a disk image.
    /// </summary>
    /// <param name="diskImageId">The disk image ID.</param>
    /// <param name="resources">Resource allocation for the sandbox.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sandbox response with ID and state.</returns>
    Task<AdcSandboxResponse> CreateSandboxFromDiskImageAsync(string diskImageId, AdcResources resources, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new sandbox from an existing snapshot.
    /// Resources are inherited from the snapshot.
    /// </summary>
    /// <param name="snapshotId">The snapshot ID to restore from.</param>
    /// <param name="labels">Optional labels to attach to the sandbox.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sandbox response with ID and state.</returns>
    Task<AdcSandboxResponse> CreateSandboxFromSnapshotAsync(string snapshotId, Dictionary<string, string>? labels = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the current state of a sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sandbox response.</returns>
    Task<AdcSandboxResponse> GetSandboxAsync(string sandboxId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Waits for a sandbox to reach the Running state.
    /// </summary>
    /// <param name="sandboxId">The sandbox ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task WaitForSandboxRunningAsync(string sandboxId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSandboxAsync(string sandboxId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts a stopped sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartSandboxAsync(string sandboxId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops a running sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopSandboxAsync(string sandboxId, CancellationToken cancellationToken = default);

    #endregion

    #region Ports

    /// <summary>
    /// Updates the port configuration for a sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox ID.</param>
    /// <param name="ports">The port specifications to configure.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The configured port information.</returns>
    Task<IReadOnlyList<AdcPortInfo>> UpdatePortsAsync(string sandboxId, IEnumerable<AdcPortSpec> ports, CancellationToken cancellationToken = default);

    #endregion

    #region Snapshots

    /// <summary>
    /// Creates a snapshot of a sandbox.
    /// </summary>
    /// <param name="sandboxId">The sandbox ID to snapshot.</param>
    /// <param name="snapshotName">The name for the snapshot.</param>
    /// <param name="labels">Optional labels to attach to the snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The snapshot ID.</returns>
    Task<string> CreateSnapshotAsync(string sandboxId, string snapshotName, Dictionary<string, string>? labels = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the details of a snapshot.
    /// </summary>
    /// <param name="snapshotId">The snapshot ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The snapshot info.</returns>
    Task<AdcSnapshotResponse> GetSnapshotAsync(string snapshotId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a snapshot.
    /// </summary>
    /// <param name="snapshotId">The snapshot ID.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSnapshotAsync(string snapshotId, CancellationToken cancellationToken = default);

    #endregion
}
