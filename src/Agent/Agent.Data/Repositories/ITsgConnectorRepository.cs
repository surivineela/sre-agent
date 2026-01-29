// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DataModels;

namespace Agent.Data.Repositories;

/// <summary>
/// Repository interface for TSG connector PAT storage
/// </summary>
public interface ITsgConnectorRepository
{
    /// <summary>
    /// Create or update a TSG connector
    /// </summary>
    Task<TsgConnectorDocument> UpsertAsync(TsgConnectorDocument connector);

    /// <summary>
    /// Get a TSG connector by name
    /// </summary>
    Task<TsgConnectorDocument?> GetByNameAsync(string name);

    /// <summary>
    /// Get all TSG connectors
    /// </summary>
    Task<IReadOnlyList<TsgConnectorDocument>> GetAllAsync();

    /// <summary>
    /// Delete a TSG connector by name
    /// </summary>
    Task<bool> DeleteAsync(string name);

    /// <summary>
    /// Get the decrypted PAT for a connector (for API calls)
    /// </summary>
    Task<string?> GetPatAsync(string name);

    /// <summary>
    /// Update the status of a connector
    /// </summary>
    Task<TsgConnectorDocument?> UpdateStatusAsync(string name, ConnectorStatus status, string? errorMessage = null);

    /// <summary>
    /// Update the clone status of a connector
    /// </summary>
    Task<TsgConnectorDocument?> UpdateCloneStatusAsync(
        string name,
        CloneStatus cloneStatus,
        string? localPath = null,
        string? latestCommit = null,
        string? errorMessage = null,
        DateTime? lastSuccessfulSync = null);
}
