// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// Service for checking the health of MCP connections before tool invocation.
/// </summary>
public class McpConnectionHealthService : IMcpConnectionHealthService
{
    private readonly IMcpConnectionEventManager _connectionManager;
    private readonly ILogger<McpConnectionHealthService> _logger;

    public McpConnectionHealthService(
        IMcpConnectionEventManager connectionManager,
        ILogger<McpConnectionHealthService> logger)
    {
        _connectionManager = connectionManager;
        _logger = logger;
    }

    public async Task<McpConnection> ValidateConnectionHealthAsync(McpConnection connection, string toolName)
    {
        if (connection == null)
        {
            throw new InvalidOperationException($"Cannot execute MCP tool '{toolName}': Connection not found");
        }

        // Check if connection is in a failed state (initialization failure - cannot reconnect)
        if (connection.Status == DataConnectorStatus.Failed)
        {
            var errorMessage = connection.ErrorMessage ?? "Connection failed";
            _logger.LogInternalWarning(
                "Rejecting tool invocation for '{ToolName}' - Connection '{ConnectionId}' is in failed state: {Error}",
                toolName,
                connection.Id,
                errorMessage);

            throw new InvalidOperationException(
                $"Cannot execute MCP tool '{toolName}': Connection '{connection.Id}' failed to initialize - {errorMessage}");
        }

        // Check if connection is disconnected - attempt to reconnect
        if (connection.Status == DataConnectorStatus.Disconnected)
        {
            _logger.LogInternalInformation(
                "Connection '{ConnectionId}' is in '{Status}' state, attempting to reconnect before executing tool '{ToolName}'",
                connection.Id,
                connection.Status,
                toolName);

            try
            {
                // Attempt to refresh/reconnect the connection
                // This creates a NEW connection instance, so we must return it
                connection = await _connectionManager.RefreshConnectionAsync(connection.Id);

                _logger.LogInternalInformation(
                    "Successfully reconnected connection '{ConnectionId}' for tool '{ToolName}'",
                    connection.Id,
                    toolName);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(
                    ex,
                    "Failed to reconnect connection '{ConnectionId}' for tool '{ToolName}'",
                    connection.Id,
                    toolName);

                throw new InvalidOperationException(
                    $"Cannot execute MCP tool '{toolName}': Connection '{connection.Id}' is disconnected and reconnection failed - {ex.Message}",
                    ex);
            }
        }

        // Check if client is null
        if (connection.Client == null)
        {
            _logger.LogInternalWarning(
                "Rejecting tool invocation for '{ToolName}' - Connection '{ConnectionId}' has null client",
                toolName,
                connection.Id);

            throw new InvalidOperationException(
                $"Cannot execute MCP tool '{toolName}': Connection '{connection.Id}' has no active client");
        }

        // Connection appears healthy
        _logger.LogInternalDebug(
            "Connection health check passed for tool '{ToolName}' on connection '{ConnectionId}'",
            toolName,
            connection.Id);

        return connection;
    }

    public McpConnection? FindConnectionByToolSignature(string toolSignature)
    {
        // MCP tool signatures follow the pattern: {connectionId}_{toolName}
        // We need to find the connection by extracting the connection ID from the tool signature
        var connections = _connectionManager.GetActiveConnections();

        // Try to find a connection where the tool signature starts with the connection ID
        foreach (var connection in connections)
        {
            if (toolSignature.StartsWith($"{connection.Id}_"))
            {
                return connection;
            }
        }

        _logger.LogInternalWarning(
            "Could not find MCP connection for tool signature '{ToolSignature}'",
            toolSignature);

        return null;
    }
}
