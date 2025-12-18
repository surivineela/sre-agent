// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// Manages the lifecycle of MCP connections with an event-driven architecture.
/// Enables dynamic runtime connection management through the API.
/// </summary>
public class McpConnectionEventManager : IMcpConnectionEventManager
{
    private readonly IMcpConnectable _backend;
    private readonly ILogger<McpConnectionEventManager> _logger;
    private readonly IMcpAuthenticationService _authService;
    private readonly ISessionTransportFactory _sessionTransportFactory;
    private readonly MCPSettings _mcpSettings;
    private readonly IAuthenticationService _coreAuthService;
    private readonly ConcurrentDictionary<string, McpConnection> _connections = new();
    private static readonly Regex _unsafeToolNameChars = new("[^a-zA-Z0-9_\\.\\-]", RegexOptions.Compiled);

    public event Func<McpConnection, Task>? ConnectionAdded;
    public event Func<string, Task>? ConnectionRemoved;

    public McpConnectionEventManager(
        IMcpConnectable backend,
        IMcpAuthenticationService authService,
        IAuthenticationService coreAuthService,
        ISessionTransportFactory sessionTransportFactory,
        IOptions<MCPSettings> mcpSettings,
        ILogger<McpConnectionEventManager> logger)
    {
        _backend = backend;
        _authService = authService;
        _coreAuthService = coreAuthService;
        _sessionTransportFactory = sessionTransportFactory;
        _mcpSettings = mcpSettings.Value;
        _logger = logger;
    }

    public async Task<McpConnection> CreateAndAddConnectionAsync(
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
        string? identity = null)
    {
        _logger.LogInternalInformation("Creating MCP connection '{Name}' of type '{Type}'", name, type);

        // Convert headers to CustomHeaders auth config if provided (LogicApps-style)
        if (headers != null && authConfig == null)
        {
            _logger.LogInternalInformation(
                "Converting direct headers to CustomHeaders authentication for connection '{Name}' (LogicApps-style)",
                name);

            authConfig = new McpAuthenticationConfig
            {
                Type = McpAuthenticationType.CustomHeaders,
                CustomHeaders = headers
            };
        }

        // Create transport based on type
        var transport = type switch
        {
            McpTransportType.Http when !string.IsNullOrEmpty(endpoint) => await CreateHttpTransportAsync(name, endpoint, authConfig),
            //"sse" when !string.IsNullOrEmpty(endpoint) => await CreateHttpTransportAsync(name, endpoint, authConfig), // Legacy SSE now uses HTTP
            McpTransportType.Stdio when !string.IsNullOrEmpty(command) && !_mcpSettings.UseSessionForStdio => new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = _unsafeToolNameChars.Replace(name, string.Empty),
                Command = command,
                Arguments = arguments ?? Array.Empty<string>(),
                WorkingDirectory = workingDirectory,
                EnvironmentVariables = envVars!
            }),
            // run local MCP servers via session pool proxy
            McpTransportType.Stdio when !string.IsNullOrEmpty(command) && _mcpSettings.UseSessionForStdio => _sessionTransportFactory.CreateSessionTransport(
                _unsafeToolNameChars.Replace(name, string.Empty),
                command,
                arguments ?? Array.Empty<string>(),
                envVars,
                identity),
            _ => throw new ArgumentException($"Invalid connection type '{type}' or missing required parameters")
        };

        // Create connection
        var connection = new McpConnection(_logger, transport)
        {
            Backend = _backend,
            Authentication = authConfig,
            Metadata = new McpConnectionMetadata
            {
                Type = type.ToString().ToLowerInvariant(),
                Endpoint = endpoint,
                Command = command,
                Arguments = arguments,
                WorkingDirectory = workingDirectory,
                Description = description,
                ServiceType = serviceType,
                EnvironmentVariables = envVars,
                Identity = identity
            }
        };

        if (_connections.ContainsKey(connection.Id))
        {
            throw new InvalidOperationException($"MCP connection '{connection.Id}' already exists");
        }

        // Initialize connection
        try
        {
            await connection.InitializeAsync();
            connection.UpdateHeartbeat();

            _logger.LogInternalInformation(
                "MCP connection '{Name}' initialized with status '{Status}' and {ToolCount} tools",
                connection.Id,
                connection.Status,
                connection.Tools?.Count ?? 0);

            // Check if initialization failed
            if (connection.Status == DataConnectorStatus.Failed)
            {
                _connections[connection.Id] = connection;

                var errorMsg = connection.ErrorMessage ?? "Unknown error during initialization";
                throw new InvalidOperationException(
                    $"Failed to connect to MCP server '{name}': {errorMsg}");
            }

            // Register tools with backend (only for successful connections)
            _backend.TryAddServer(connection);

            // Track connection
            _connections[connection.Id] = connection;

            // Fire event
            if (ConnectionAdded != null)
            {
                await ConnectionAdded.Invoke(connection);
            }

            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to initialize MCP connection '{Name}'", name);
            throw;
        }
    }

    public async Task<bool> RemoveConnectionAsync(string connectionId)
    {
        _logger.LogInternalInformation("Removing MCP connection '{ConnectionId}'", connectionId);

        if (!_connections.TryRemove(connectionId, out var connection))
        {
            _logger.LogInternalWarning("Connection '{ConnectionId}' not found", connectionId);
            return false;
        }

        // Remove from backend
        _backend.TryRemoveServer(connection);

        // Fire event
        if (ConnectionRemoved != null)
        {
            await ConnectionRemoved.Invoke(connectionId);
        }

        _logger.LogInternalInformation("Successfully removed MCP connection '{ConnectionId}'", connectionId);
        return true;
    }

    public IReadOnlyCollection<McpConnection> GetActiveConnections()
    {
        return _connections.Values.ToList().AsReadOnly();
    }

    public Task<McpConnection?> GetConnectionAsync(string connectionId)
    {
        _connections.TryGetValue(connectionId, out var connection);
        return Task.FromResult(connection);
    }

    public async Task<McpConnection> RefreshConnectionAsync(string connectionId)
    {
        _logger.LogInternalInformation("Refreshing MCP connection '{ConnectionId}'", connectionId);

        // Get existing connection
        if (!_connections.TryGetValue(connectionId, out var existingConnection))
        {
            throw new ArgumentException($"Connection '{connectionId}' not found");
        }

        if (existingConnection.Metadata == null)
        {
            throw new InvalidOperationException($"Connection '{connectionId}' has no metadata for refresh");
        }

        try
        {
            // Store connection details for recreation
            var name = existingConnection.Id;
            var metadata = existingConnection.Metadata;
            var authConfig = existingConnection.Authentication;

            _logger.LogInternalInformation(
                "Recreating connection '{ConnectionId}' with type '{Type}'",
                connectionId,
                metadata.Type);

            // Remove old connection first
            _backend.TryRemoveServer(existingConnection);
            _connections.TryRemove(connectionId, out _);

            // Fire removal event
            if (ConnectionRemoved != null)
            {
                await ConnectionRemoved.Invoke(connectionId);
            }

            // Parse the type string back to enum for reconnection
            if (!Enum.TryParse<McpTransportType>(metadata.Type, ignoreCase: true, out var transportType))
            {
                throw new InvalidOperationException($"Invalid transport type '{metadata.Type}' in connection metadata");
            }

            // Create new connection with same parameters
            var newConnection = await CreateAndAddConnectionAsync(
                name,
                transportType,
                metadata.Endpoint,
                metadata.Command,
                metadata.Arguments,
                metadata.WorkingDirectory,
                authConfig,
                headers: null,
                metadata.Description,
                metadata.ServiceType,
                metadata.EnvironmentVariables,
                metadata.Identity);

            _logger.LogInternalInformation(
                "Successfully refreshed MCP connection '{ConnectionId}' - New tool count: {ToolCount}",
                connectionId,
                newConnection.Tools?.Count ?? 0);

            return newConnection;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to refresh MCP connection '{ConnectionId}'", connectionId);
            throw;
        }
    }

    private async Task<IClientTransport> CreateHttpTransportAsync(
        string name,
        string endpoint,
        McpAuthenticationConfig? authConfig)
    {
        var sanitizedName = _unsafeToolNameChars.Replace(name, string.Empty);
        var endpointUri = new Uri(endpoint);

        // Use AuthenticatedHttpClientTransportFactory to create transport with authentication
        return await AuthenticatedHttpClientTransportFactory.CreateHttpTransportAsync(
            sanitizedName,
            endpointUri,
            authConfig,
            _coreAuthService,
            _logger);
    }

    public async Task VerifyConnectionsAsync()
    {
        var connections = _connections.Values.ToList();

        if (connections.Count == 0)
        {
            return;
        }

        _logger.LogInternalDebug("Verifying {Count} dynamically added MCP connections", connections.Count);

        var verificationTasks = connections.Where(c => c.Status == DataConnectorStatus.Connected).Select(async connection =>
        {
            try
            {
                if (connection.Client == null)
                {
                    _logger.LogInternalWarning("MCP client is null for connection '{ConnectionId}', marking as failed", connection.Id);
                    connection.MarkAsFailed("MCP client is null");
                    return;
                }

                if (connection.Status != DataConnectorStatus.Connected)
                {
                    _logger.LogInternalInformation(
                        "Connection '{ConnectionId}' is not in Connected state (current: '{Status}'), skipping ping",
                        connection.Id,
                        connection.Status);
                    return;
                }

                // Attempt to ping the connection
                var completed = false;

                try
                {
                    using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(_mcpSettings.PingTimeoutInSeconds));
                    await connection.Client.PingAsync(cts.Token).ContinueWith(t => completed = t.IsCompletedSuccessfully);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, "Exception during ping for '{ConnectionId}'", connection.Id);
                }

                if (!completed)
                {
                    _logger.LogInternalWarning("Ping timed out for '{ConnectionId}', marking as disconnected", connection.Id);
                    connection.IncrementPingFailures();
                    connection.MarkAsDisconnected($"Ping timeout after {_mcpSettings.PingTimeoutInSeconds} seconds");
                }
                else
                {
                    _logger.LogInternalInformation("Successfully pinged '{ConnectionId}'", connection.Id);
                    connection.UpdateHeartbeat();
                    connection.ResetPingFailures();
                    // If connection was previously disconnected due to ping issues, mark it as healthy again
                    if (connection.Status == DataConnectorStatus.Disconnected)
                    {
                        connection.MarkAsConnected();
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Ping failed for '{ConnectionId}', marking as disconnected", connection.Id);
                connection.IncrementPingFailures();
                connection.MarkAsDisconnected($"Ping failed: {ex.Message}");
            }
        });

        await Task.WhenAll(verificationTasks);
    }
}
