// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using Agent.Core.Configuration;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

/// <summary>
/// Responsible for initializing the MCPMetaAgent instance with the provided MCP servers,
/// making sure the connections stay open, and removing tools if the connections close.
/// 
/// Does not yet support taking Prompt, Resource, Tool, or Utility change notifications and dynamically modifying agents. Only supports disconnecting and reconnecting to servers.
/// </summary>
public class MCPMetaAgentManagementService : IHostedService, IDisposable
{
    private readonly MCPMetaAgent _mcpMetaAgent;
    private readonly MCPSettings _mcpSettings;
    private readonly ILogger<MCPMetaAgentManagementService> _logger;
    private Timer? _connectionVerificationTimer;
    private readonly SemaphoreSlim _connectionVerificationTimerLock = new(1, 1);
    private Timer? _reconnectTimer;
    private readonly SemaphoreSlim _reconnectTimerLock = new(1, 1);
    private readonly ILoggerFactory _loggerFactory;
    private readonly IMcpConnectable _mcpToolsRepository;

    private static Regex _unsafeToolNameChars = new Regex("[^a-zA-Z0-9_\\.\\-]", RegexOptions.Compiled);

    /// <summary>
    /// URLs mapped to connections
    /// </summary>
    private ConcurrentDictionary<string, McpConnection> _activeConnections = new ConcurrentDictionary<string, McpConnection>();

    public MCPMetaAgentManagementService(
        MCPMetaAgent mcpMetaAgent,
        MCPSettings mcpSettings,
        IMcpConnectable mcpToolsRepository,
        ILogger<MCPMetaAgentManagementService> logger,
        ILoggerFactory loggerFactory)
    {
        _mcpMetaAgent = mcpMetaAgent;
        _mcpSettings = mcpSettings;
        _logger = logger;
        _loggerFactory = loggerFactory;
        _mcpToolsRepository = mcpToolsRepository;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInternalInformation("Starting MCPMetaAgent async initialization");

            // Block app startup until we have tried to connect once
            await ReconnectTimerWork();

            StartReconnectTimer(cancellationToken);
            StartConnectionVerificationTimer(cancellationToken);

            _logger.LogInternalInformation("Completed MCPMetaAgent async initialization");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to initialize MCPAgent instance");
        }
    }

    /// <summary>
    /// Kicks off a timer which periodically verifies the connection to the MCP servers.
    /// </summary>
    public void StartConnectionVerificationTimer(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Starting connection verification timer");

        _connectionVerificationTimer = new Timer(async _ =>
        {
            if (!_connectionVerificationTimerLock.Wait(0)) return;

            try
            {
                var connections = _activeConnections.Values;
                var pingTasks = connections.Select(async connection => await RemoveConnectionIfFailsPing(connection));
                await Task.WhenAll(pingTasks);
            }
            finally
            {
                _connectionVerificationTimerLock.Release();
            }
        }, null, TimeSpan.FromSeconds(_mcpSettings.PingIntervalInSeconds), TimeSpan.FromSeconds(_mcpSettings.PingIntervalInSeconds));
    }

    private async Task RemoveConnectionIfFailsPing(McpConnection connection)
    {
        try
        {
            IMcpClient? client = connection.Client;
            if (client == null)
            {
                throw new Exception($"MCP client is null for connection '{connection.Id}'. This should not happen.");
            }

            if (connection.ClientTransport is StdioClientTransport)
            {
                _logger.LogInternalWarning("StdioClientTransport is not supported for pinging");
                return;
            }

            bool completed = false;
            var pingTask = client.PingAsync().ContinueWith(t => completed = !t.IsFaulted);

            // Wait for the ping task to complete or timeout after 10 seconds
            await Task.WhenAny(pingTask, Task.Delay(TimeSpan.FromSeconds(_mcpSettings.PingTimeoutInSeconds)));

            if (!completed)
            {
                _logger.LogInternalWarning("Ping timed out for '{connection}', removing associated agent", connection);
                connection.Backend.TryRemoveServer(connection);
                _activeConnections.TryRemove(connection.Id, out McpConnection? _);
            }
            else
            {
                _logger.LogTrace("Successfully pinged '{connection}'", connection);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Ping failed for MCP agent '{connection}', removing associated agent", connection);
            connection.Backend.TryRemoveServer(connection);
            _activeConnections.TryRemove(connection.Id, out McpConnection? _);
        }
    }

    /// <summary>
    /// Kicks off a timer which periodically attempts to reconnect to any disconnected MCP servers.
    /// </summary>
    public void StartReconnectTimer(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Starting reconnect timer");

        _reconnectTimer = new Timer(async _ => await ReconnectTimerWork(), null, TimeSpan.FromSeconds(_mcpSettings.PingIntervalInSeconds), TimeSpan.FromSeconds(_mcpSettings.PingIntervalInSeconds));
    }

    private async Task ReconnectTimerWork()
    {
        if (!_reconnectTimerLock.Wait(0)) return;

        try
        {
            var disconnectedMcpMetaAgentUrls = _mcpSettings.IsolatedServers.Where(url => !_activeConnections.ContainsKey(url));
            var mcpMetaAgentTasks = disconnectedMcpMetaAgentUrls.Select(async url => await AddConnectionIfSuccessful(CreateMcpClientSseTransport(url), _mcpMetaAgent));

            var disconnectedToolsRepositoryUrls = _mcpSettings.SharedServers.Where(url => !_activeConnections.ContainsKey(url));

            //  for now, collect a separate set of MCP tools to expose to the meta agent
            //  todo - figure out how we want MCP tools injected into subagents too.
            //var toolsRepositoryTasks = disconnectedToolsRepositoryUrls.Select(async url => await AddConnectionIfSuccessful(url, _toolsRepository));
            var mcpToolsRepositoryTasks = disconnectedToolsRepositoryUrls.Select(async url => await AddConnectionIfSuccessful(CreateMcpClientSseTransport(url), _mcpToolsRepository));

            await Task.WhenAll(mcpMetaAgentTasks.Concat(mcpToolsRepositoryTasks));
        }
        finally
        {
            _reconnectTimerLock.Release();
        }
    }

    private IClientTransport CreateMcpClientSseTransport(string url)
        => new SseClientTransport(new()
        {
            Name = _unsafeToolNameChars.Replace(url, string.Empty),
            Endpoint = new Uri(url),
            TransportMode = HttpTransportMode.AutoDetect
        });

    private IClientTransport CreateMcpClientStdioTransport(string name, string command, string[] arguments)
        => new StdioClientTransport(new()
        {
            Name = _unsafeToolNameChars.Replace(name, string.Empty),
            Command = command,
            Arguments = arguments
        });

    private async Task AddConnectionIfSuccessful(IClientTransport clientTransport, IMcpConnectable connectable)
    {
        McpConnection connection = new McpConnection(clientTransport)
        {
            McpLoggerFactory = _loggerFactory,
            Backend = connectable
        };

        await connection.InitializeAsync().ContinueWith(t =>
        {
            if (t.IsFaulted)
            {
                _logger.LogInternalInformation("Failed to initialize connection '{connection}'", connection);
            }
            else
            {
                _logger.LogInternalInformation("Initialized connection '{connection}'", connection);
                connectable.TryAddServer(connection);
                _activeConnections.TryAdd(connection.Id, connection);
            }
        });
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation($"Stopping...");

        _connectionVerificationTimer?.Change(Timeout.Infinite, 0);
        _reconnectTimer?.Change(Timeout.Infinite, 0);

        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _connectionVerificationTimer?.Dispose();
        _reconnectTimer?.Dispose();
    }
}
