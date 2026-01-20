// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Logging;
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
        string? identity = null,
        bool useLocalStdio = false)
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
            // Use local stdio when: (1) UseSessionForStdio=false, OR (2) useLocalStdio=true
            McpTransportType.Stdio when !string.IsNullOrEmpty(command) && (!_mcpSettings.UseSessionForStdio || useLocalStdio) => new StdioClientTransport(new StdioClientTransportOptions
            {
                Name = _unsafeToolNameChars.Replace(name, string.Empty),
                Command = command,
                Arguments = arguments ?? Array.Empty<string>(),
                WorkingDirectory = workingDirectory,
                EnvironmentVariables = envVars!
            }),
            // Run local MCP servers via session pool proxy (only when UseSessionForStdio=true AND useLocalStdio=false)
            McpTransportType.Stdio when !string.IsNullOrEmpty(command) && _mcpSettings.UseSessionForStdio && !useLocalStdio => _sessionTransportFactory.CreateSessionTransport(
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
                Type = type,
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

                // Log failed MCP connection
                _logger.LogAgentAction(
                    action: AgentActionEvents.McpConnection,
                    parameter: BuildAgentActionParameter(name, type, endpoint, command, arguments),
                    status: AgentActionStatus.Fail,
                    duration: 0,
                    threadId: string.Empty);

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

            // Log successful MCP connection
            _logger.LogAgentAction(
                action: AgentActionEvents.McpConnection,
                parameter: BuildAgentActionParameter(name, type, endpoint, command, arguments),
                status: AgentActionStatus.Success,
                duration: 0,
                threadId: string.Empty);

            return connection;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to initialize MCP connection '{Name}'", name);
            _logger.LogAgentAction(
                action: AgentActionEvents.McpConnection,
                parameter: BuildAgentActionParameter(name, type, endpoint, command, arguments),
                status: AgentActionStatus.Fail,
                duration: 0,
                threadId: string.Empty);
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

            // Create new connection with same parameters
            var newConnection = await CreateAndAddConnectionAsync(
                name,
                metadata.Type,
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

        var verificationTasks = connections.Select(connection => connection.PingAsync(_mcpSettings.PingTimeoutInSeconds));

        await Task.WhenAll(verificationTasks);
    }

    private static string BuildAgentActionParameter(string name, McpTransportType type, string? endpoint, string? command, string[]? arguments)
    {
        string mcpName;
        if (type == McpTransportType.Http)
        {
            var uri = new Uri(endpoint!);
            // remove the query and fragment parts to avoid leaking sensitive info
            mcpName = uri.GetLeftPart(UriPartial.Path);
        }
        else
        {
            // try to extract the exact tool name from the command
            mcpName = command!;
            if (mcpName == "npx" || mcpName == "node" ||
                mcpName == "uvx" || mcpName == "python" || mcpName == "python3" ||
                mcpName == "dotnet" || mcpName == "dnx")
            {
                if (arguments != null && arguments.Length > 0)
                {
                    mcpName += " " + arguments.First(arg => !arg.StartsWith('-'));
                }
            }
        }

        var parameter = new
        {
            name,
            type,
            endpoint,
            command,
            arguments,
            mcpName,
        };
        return JsonSerializer.Serialize(parameter);
    }
}
