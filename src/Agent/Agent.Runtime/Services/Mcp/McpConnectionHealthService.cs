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

    public void ValidateConnectionHealth(McpConnection connection, string toolName)
    {
        if (connection == null)
        {
            throw new InvalidOperationException($"Cannot execute MCP tool '{toolName}': Connection not found");
        }

        // Check if connection is in a failed state
        if (connection.Status == McpConnectionStatus.Failed)
        {
            var errorMessage = connection.ErrorMessage ?? "Connection failed";
            _logger.LogInternalWarning(
                "Rejecting tool invocation for '{ToolName}' - Connection '{ConnectionId}' is unhealthy: {Error}",
                toolName,
                connection.Id,
                errorMessage);

            throw new InvalidOperationException(
                $"Cannot execute MCP tool '{toolName}': Connection '{connection.Id}' is unhealthy - {errorMessage}");
        }

        // Check if connection is disconnected
        if (connection.Status == McpConnectionStatus.Disconnected)
        {
            _logger.LogInternalWarning(
                "Rejecting tool invocation for '{ToolName}' - Connection '{ConnectionId}' is disconnected",
                toolName,
                connection.Id);

            throw new InvalidOperationException(
                $"Cannot execute MCP tool '{toolName}': Connection '{connection.Id}' is disconnected");
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