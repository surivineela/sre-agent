// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// Orchestrates the complete lifecycle of MCP-generated agents by coordinating between
/// connection management, agent generation, and the agent factory.
/// </summary>
public class McpAgentManagementService : IHostedService, IAsyncDisposable
{
    private readonly IMcpConnectionEventManager _connectionManager;
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly IAgentFactory<AgentContext> _agentFactory;
    private readonly IMcpConnectable _mcpToolsRepository;
    private readonly MCPSettings _mcpSettings;
    private readonly ILogger<McpAgentManagementService> _logger;
    private Timer? _heartbeatTimer;
    private readonly SemaphoreSlim _heartbeatLock = new(1, 1);

    public McpAgentManagementService(
        IMcpConnectionEventManager connectionManager,
        IToolFactory<AgentContext> toolFactory,
        IAgentFactory<AgentContext> agentFactory,
        IMcpConnectable mcpToolsRepository,
        IOptions<MCPSettings> mcpSettings,
        ILogger<McpAgentManagementService> logger)
    {
        _connectionManager = connectionManager;
        _toolFactory = toolFactory;
        _agentFactory = agentFactory;
        _mcpToolsRepository = mcpToolsRepository;
        _mcpSettings = mcpSettings.Value;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Starting MCP Agent Management Service");

        if (!_mcpSettings.Enabled)
        {
            _logger.LogInternalInformation("MCP is disabled via settings. MCP Agent Management Service will not start.");
            return Task.CompletedTask;
        }

        // Subscribe to connection events
        _connectionManager.ConnectionAdded += OnConnectionAdded;
        _connectionManager.ConnectionRemoved += OnConnectionRemoved;

        // Start heartbeat verification timer for dynamically added connections
        StartHeartbeatTimer();

        _logger.LogInternalInformation("MCP Agent Management Service started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInternalInformation("Stopping MCP Agent Management Service");

        // Stop heartbeat timer
        _heartbeatTimer?.Change(Timeout.Infinite, 0);

        // Unsubscribe from events
        _connectionManager.ConnectionAdded -= OnConnectionAdded;
        _connectionManager.ConnectionRemoved -= OnConnectionRemoved;

        _logger.LogInternalInformation("MCP Agent Management Service stopped");
        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        // Stop heartbeat timer
        if (_heartbeatTimer != null)
        {
            await _heartbeatTimer.DisposeAsync();
        }

        _heartbeatLock?.Dispose();
    }

    /// <summary>
    /// Starts a timer to periodically verify heartbeats for dynamically added MCP connections.
    /// </summary>
    private void StartHeartbeatTimer()
    {
        _logger.LogInternalInformation(
            "Starting heartbeat verification timer for dynamic connections with {Interval}s interval",
            _mcpSettings.PingIntervalInSeconds);

        _heartbeatTimer = new Timer(async _ =>
        {
            if (!_heartbeatLock.Wait(0)) return;

            try
            {
                await _connectionManager.VerifyConnectionsAsync();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Error during dynamic connection heartbeat verification");
            }
            finally
            {
                _heartbeatLock.Release();
            }
        }, null, TimeSpan.FromSeconds(_mcpSettings.PingIntervalInSeconds), TimeSpan.FromSeconds(_mcpSettings.PingIntervalInSeconds));
    }

    private async Task OnConnectionAdded(McpConnection connection)
    {
        _logger.LogInternalInformation("Handling connection added: '{ConnectionId}'", connection.Id);

        try
        {
            // Refresh MCP tools in ToolFactory to include new connection's tools
            await _toolFactory.RefreshMcpToolsAsync();
            
            // Note: Agent Builder is now responsible for explicitly adding MCP tools to agents
            // Tools are available in the MCP tools repository for agent builders to reference
            
            _logger.LogInternalInformation(
                "Refreshed MCP tools in ToolFactory for connection '{ConnectionId}'. Tools from this connection are now available in the MCP repository for agent builders to use.",
                connection.Id);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "Failed to handle connection added for '{ConnectionId}'",
                connection.Id);
        }
    }

    private async Task OnConnectionRemoved(string connectionId)
    {
        _logger.LogInternalInformation("Handling connection removed: '{ConnectionId}'", connectionId);

        try
        {
            // Refresh MCP tools in ToolFactory to remove disconnected connection's tools
            await _toolFactory.RefreshMcpToolsAsync();

            // Note: Agent Builder is now responsible for managing tools in agents
            // Tools are removed from the MCP tools repository automatically

            _logger.LogInternalInformation(
                "Refreshed MCP tools in ToolFactory after removing connection '{ConnectionId}'",
                connectionId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(
                ex,
                "Failed to handle connection removed for '{ConnectionId}'",
                connectionId);
        }
    }

    // REMOVED: Automatic tool addition to meta_agent
    // Agent Builder is now responsible for explicitly choosing which MCP tools to add to agents
    // This method previously automatically added the first 20 tools from each MCP connection to meta_agent
    // 
    // /// <summary>
    // /// Dynamically adds MCP tools to the meta_agent's FactoryTools list.
    // /// This ensures the meta_agent can resolve and use MCP tools at runtime.
    // /// </summary>
    // private void AddMcpToolsToMetaAgent()
    // {
    //     try
    //     {
    //         // Try to get the meta_agent
    //         Agent<AgentContext> metaAgent;
    //         try
    //         {
    //             metaAgent = _agentFactory.GetAgent("meta_agent");
    //         }
    //         catch
    //         {
    //             _logger.LogInternalWarning("meta_agent does not exist, cannot add MCP tools");
    //             return;
    //         }
    //         
    //         // Get all MCP tool names from the repository
    //         var mcpToolNames = _mcpToolsRepository.GetAllFunctions()
    //             .Select(f => f.Name)
    //             .ToList();
    //
    //         // Remove old MCP tools (those prefixed with connection IDs)
    //         // MCP tools have format: {connectionId}_{toolName}
    //         metaAgent.FactoryTools.RemoveAll(tool => tool.Contains("_") && 
    //             _mcpToolsRepository.GetAllFunctions().Any(f => f.Name == tool));
    //
    //         // Add current MCP tools
    //         metaAgent.FactoryTools.AddRange(mcpToolNames);
    //
    //         _logger.LogInternalInformation(
    //             "Updated meta_agent with {Count} MCP tools",
    //             mcpToolNames.Count);
    //     }
    //     catch (Exception ex)
    //     {
    //         _logger.LogInternalError(ex, "Failed to add MCP tools to meta_agent");
    //     }
    // }
}
