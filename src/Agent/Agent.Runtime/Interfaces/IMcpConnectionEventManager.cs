// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Models;

namespace Agent.Runtime.Interfaces;

/// <summary>
/// Manages the lifecycle of MCP connections with an event-driven architecture.
/// </summary>
public interface IMcpConnectionEventManager
{
    /// <summary>
    /// Fired when a new MCP connection is successfully added.
    /// </summary>
    event Func<McpConnection, Task>? ConnectionAdded;

    /// <summary>
    /// Fired when an MCP connection is removed.
    /// </summary>
    event Func<string, Task>? ConnectionRemoved;

    /// <summary>
    /// Creates and adds a new MCP connection.
    /// </summary>
    Task<McpConnection> CreateAndAddConnectionAsync(
        string name,
        McpTransportType type,
        string? endpoint = null,
        string? command = null,
        string[]? arguments = null,
        string? workingDirectory = null,
        McpAuthenticationConfig? authConfig = null,
        Dictionary<string, string>? headers = null,
        string? description = null,
        string? serviceType = null,
        Dictionary<string, string>? envVars = null,
        string? identity = null);

    /// <summary>
    /// Removes an existing MCP connection by ID.
    /// </summary>
    Task<bool> RemoveConnectionAsync(string connectionId);

    /// <summary>
    /// Gets all active connections.
    /// </summary>
    IReadOnlyCollection<McpConnection> GetActiveConnections();

    /// <summary>
    /// Gets a specific connection by ID.
    /// </summary>
    Task<McpConnection?> GetConnectionAsync(string connectionId);

    /// <summary>
    /// Refreshes an existing MCP connection by reconnecting to the server and reloading tools.
    /// </summary>
    Task<McpConnection> RefreshConnectionAsync(string connectionId);

    /// <summary>
    /// Verifies all active connections by pinging them and removing failed connections.
    /// Updates heartbeat timestamps for successful pings.
    /// </summary>
    Task VerifyConnectionsAsync();
}
