// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Adc;

namespace Agent.Core.Interfaces;

/// <summary>
/// High-level service for managing agent sandboxes.
/// Handles base snapshot lifecycle, per-thread sandbox provisioning, and thread snapshots.
/// Uses <see cref="IAdcApiClient"/> for ADC operations and Cosmos DB for persistence.
/// </summary>
public interface IAgentSandboxService
{
    #region Base Snapshot Management

    /// <summary>
    /// Ensures a base snapshot exists and is current with the configured base image.
    /// If the base image has changed or no snapshot exists, creates a new one.
    /// </summary>
    /// <param name="initializer">Optional logic to run after initial base sandbox is active. Used to install common tools.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The current base snapshot info.</returns>
    Task<AdcBaseSnapshotInfo> EnsureBaseSnapshotAsync(ISandboxInitializer? initializer = null, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the current base snapshot info.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The base snapshot info, or null if not yet created.</returns>
    Task<AdcBaseSnapshotInfo?> GetBaseSnapshotAsync(CancellationToken cancellationToken = default);

    #endregion

    #region Thread Sandbox Management

    /// <summary>
    /// Creates a new sandbox for the thread from the specified snapshot.
    /// If snapshotId is null, uses the base snapshot.
    /// Deletes any existing sandbox for the thread first.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="snapshotId">Optional snapshot ID to create sandbox from. If null, uses base snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sandbox info.</returns>
    Task<AdcSandboxInfo> CreateSandboxForThreadAsync(
        string threadId,
        string? snapshotId = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the sandbox info for a thread without creating one if it doesn't exist.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sandbox info, or null if no sandbox exists for the thread.</returns>
    Task<AdcSandboxInfo?> GetSandboxForThreadAsync(string threadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the endpoint URL for a thread's sandbox.
    /// Returns the sandbox endpoint if one exists, otherwise returns the configured default endpoint.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The sandbox endpoint URL.</returns>
    Task<string> GetSandboxEndpointAsync(string threadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes the active sandbox for the thread.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSandboxForThreadAsync(string threadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Starts the thread's sandbox if stopped.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StartSandboxAsync(string threadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Stops the thread's sandbox.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task StopSandboxAsync(string threadId, CancellationToken cancellationToken = default);

    #endregion

    #region Thread Snapshot Management

    /// <summary>
    /// Creates a snapshot of the current state of the thread's sandbox.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="snapshotName">Name for the snapshot.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The created snapshot info.</returns>
    Task<AdcSnapshotInfo> CreateSnapshotAsync(string threadId, string snapshotName, CancellationToken cancellationToken = default);

    /// <summary>
    /// Lists all snapshots available for the thread.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>The list of available snapshots.</returns>
    Task<IEnumerable<AdcSnapshotInfo>> GetSnapshotsForThreadAsync(string threadId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes a specific thread snapshot.
    /// </summary>
    /// <param name="threadId">The thread identifier.</param>
    /// <param name="snapshotId">The snapshot ID to delete.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    Task DeleteSnapshotAsync(string threadId, string snapshotId, CancellationToken cancellationToken = default);

    #endregion
}
