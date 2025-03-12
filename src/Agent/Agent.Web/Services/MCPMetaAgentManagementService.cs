using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Runtime.SubAgents;
using McpDotNet.Client;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

/// <summary>
/// Responsible for initializing the MCPMetaAgent instance with the provided MCP servers,
/// making sure the connections stay open, and removing tools if the connections close.
/// </summary>
public class MCPMetaAgentManagementService : IHostedService, IDisposable
{
    private readonly MCPMetaAgent _mcpMetaAgent;
    private readonly MCPSettings _mcpSettings;
    private readonly ILogger<MCPMetaAgentManagementService> _logger;
    private  Timer _connectionVerificationTimer;
    private bool _connectionVerificationTimerIsRunning;
    private Timer _reconnectTimer;
    private bool _reconnectTimerIsRunning;
    private ConcurrentHashSet<string> _connectedMCPServers = new ConcurrentHashSet<string>();
    private ConcurrentHashSet<string> _disconnectedMCPServers = new ConcurrentHashSet<string>();
    private static Regex _unsafeToolNameChars = new Regex("[^a-zA-Z0-9_\\.\\-]", RegexOptions.Compiled);
    private ConcurrentDictionary<string, string> _mcpServers = new ConcurrentDictionary<string, string>();

    public MCPMetaAgentManagementService(
        MCPMetaAgent mcpMetaAgent,
        MCPSettings mcpSettings,
        ILogger<MCPMetaAgentManagementService> logger)
    {
        _mcpMetaAgent = mcpMetaAgent;
        _mcpSettings = mcpSettings;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInformation("Starting MCPMetaAgent async initialization");

            foreach(string url in _mcpSettings.Servers)
            {
                string key = _unsafeToolNameChars.Replace(url, "");
                _mcpServers[key] = url;
                _disconnectedMCPServers.Add(key);
            }

            StartReconnectTimer(cancellationToken);
            StartConnectionVerificationTimer(cancellationToken);

            _logger.LogInformation("Completed MCPMetaAgent async initialization");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to initialize MCPAgent instance");
        }
    }

    /// <summary>
    /// Kicks off a timer which periodically verifies the connection to the MCP servers.
    /// </summary>
    public void StartConnectionVerificationTimer(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting connection verification timer");

        _connectionVerificationTimer = new Timer(async _ =>
        {
            try
            {
                if (_connectionVerificationTimerIsRunning) return;

                _connectionVerificationTimerIsRunning = true;

                var agentKeys = _mcpServers.Keys.ToList().Where(k => _connectedMCPServers.Contains(k));
                var pingTasks = agentKeys.Select(async key =>
                {
                    try
                    {
                        bool fetched = _mcpMetaAgent.TryGetAgent(key, out MCPAgent? agent);
                        IMcpClient client = agent?.MCPClient ?? throw new Exception("Agent not found");

                        bool completed = false;
                        var pingTask = client.PingAsync().ContinueWith(t => completed = !t.IsFaulted);

                        // Wait for the ping task to complete or timeout after 10 seconds
                        await Task.WhenAny(pingTask, Task.Delay(TimeSpan.FromSeconds(_mcpSettings.PingTimeoutInSeconds)));

                        if (!completed)
                        {
                            _logger.LogWarning("Ping timed out for MCP agent '{key}', removing agent.", key);
                            _mcpMetaAgent.TryRemoveServer(key);
                            _connectedMCPServers.Remove(key);
                            _disconnectedMCPServers.Add(key);
                        } else
                        {
                            _logger.LogTrace("Successfully pinged {key}", key);
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(ex, "Ping failed for MCP agent '{Key}', removing agent.", key);
                        _mcpMetaAgent.TryRemoveServer(key);
                    }
                });
                await Task.WhenAll(pingTasks);
            }
            finally
            {
                _connectionVerificationTimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(_mcpSettings.PingIntervalInSeconds));
    }

    /// <summary>
    /// Kicks off a timer which periodically attempts to reconnect to any disconnected MCP servers.
    /// </summary>
    public void StartReconnectTimer(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Starting reconnect timer");

        _reconnectTimer = new Timer(async _ =>
        {
            try
            {
                if (_reconnectTimerIsRunning) return;

                _reconnectTimerIsRunning = true;

                var agentKeys = _mcpServers.Keys.ToList().Where(k => _disconnectedMCPServers.Contains(k));

                var tasks = agentKeys.Select(async key => {
                    string mcpURL = _mcpServers[key];
                    await _mcpMetaAgent.AddServer(key, mcpURL).ContinueWith(t =>
                    {
                        // Exception will be logged by meta agent
                        if (t.IsFaulted)
                        {
                            _connectedMCPServers.Remove(key);
                            _disconnectedMCPServers.Add(key);
                            _mcpMetaAgent.TryRemoveServer(key);
                        }
                        else
                        {
                            _logger.LogInformation("Connected to agent {key}", key);
                            _connectedMCPServers.Add(key);
                            _disconnectedMCPServers.Remove(key);
                        }
                    });
                });
                await Task.WhenAll(tasks);
            }
            finally
            {
                _reconnectTimerIsRunning = false;
            }
        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(_mcpSettings.PingIntervalInSeconds));
    }

    public Task StopAsync(CancellationToken cancellationToken) {
        _logger.LogInformation($"Stopping...");

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