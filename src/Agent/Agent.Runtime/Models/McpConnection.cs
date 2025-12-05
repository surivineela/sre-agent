// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Runtime.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Agent.Runtime.Models;

/// <summary>
/// Represents an active connection to an MCP server and the resources that are available on that server.
/// </summary>
public class McpConnection
{
    private readonly ILogger _logger;
    public required IMcpConnectable Backend { get; init; }

    public string Id { get; private set; }
    public IList<AITool>? Tools { get; private set; }
    public string? ServerInstructions { get; private set; }
    public McpClient? Client { get; private set; }
    public IClientTransport ClientTransport { get; private set; }
    public DateTimeOffset LastHeartbeat { get; private set; } = DateTimeOffset.MinValue;

    /// <summary>
    /// Connection status indicating the health of the MCP connection.
    /// </summary>
    public DataConnectorStatus Status { get; private set; } = DataConnectorStatus.Initializing;

    /// <summary>
    /// Error message if connection failed.
    /// </summary>
    public string? ErrorMessage { get; private set; }

    /// <summary>
    /// Number of consecutive ping failures for this connection.
    /// Reset to 0 on successful ping. Used to track connection health.
    /// </summary>
    public int ConsecutivePingFailures { get; private set; } = 0;

    /// <summary>
    /// Authentication configuration for MCP server requests.
    /// </summary>
    public McpAuthenticationConfig? Authentication { get; set; }

    /// <summary>
    /// Connection metadata for refresh operations.
    /// </summary>
    public McpConnectionMetadata? Metadata { get; set; }

    private bool _initialized = false;
    private static Regex _unsafeToolNameChars = new Regex("[^a-zA-Z0-9_\\.\\-]", RegexOptions.Compiled);

    public McpConnection(ILogger logger, IClientTransport clientTransport)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (clientTransport == null)
        {
            throw new ArgumentException("The provided client transport is null.", nameof(clientTransport));
        }

        Id = _unsafeToolNameChars.Replace(clientTransport.Name, "");
        ClientTransport = clientTransport;
    }

    public async Task InitializeAsync()
    {
        // For debugging purposes, with mcpClient.
        using ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
        {
            //builder.AddConsole();     // add console logging
            //builder.SetMinimumLevel(LogLevel.Debug);
        });

        try
        {
            if (_initialized)
            {
                return;
            }

            McpClientOptions options = new()
            {
                ClientInfo = new() { Name = Id, Version = "1.0.0" }
            };

            _logger.LogInternalInformation("Attempting to connect to {endpoint}", Id);

            try
            {
                Client = await McpClient.CreateAsync(
                    ClientTransport,
                    options,
                    loggerFactory: loggerFactory
                );

                Tools = (await Client.ListToolsAsync()).ToList<AITool>();

                foreach (var tool in Tools)
                {
                    _logger.LogInternalInformation("Imported tool: {tool} from MCP server {server}", tool.Name, Id);
                }

                if (!string.IsNullOrEmpty(Client.ServerInstructions))
                {
                    ServerInstructions = Client.ServerInstructions;
                }

                Status = DataConnectorStatus.Connected;
                _initialized = true;
            }
            catch (HttpRequestException hre)
            {
                // Network/HTTP errors indicate server unreachable or authentication failure
                _logger.LogInternalError(hre, "HTTP error connecting to MCP server at {endpoint}", Id);

                Status = DataConnectorStatus.Failed;
                ErrorMessage = hre.StatusCode.HasValue
                    ? $"HTTP {(int)hre.StatusCode.Value} {hre.StatusCode.Value}: {hre.Message}"
                    : $"Network error: {hre.Message}";

                Tools = new List<AITool>();
                _initialized = true;
            }
            catch (System.Net.Sockets.SocketException se)
            {
                // Socket errors indicate network/connectivity issues
                _logger.LogInternalError(se, "Socket error connecting to MCP server at {endpoint}", Id);

                Status = DataConnectorStatus.Failed;
                ErrorMessage = $"Connection failed: {se.Message}";

                Tools = new List<AITool>();
                _initialized = true;
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to initialize connection to {endpoint}", Id);
            throw;
        }
    }

    // Added ToString override
    public override string ToString()
    {
        return $"McpConnection: Id={Id}";
    }

    /// <summary>
    /// Updates the last heartbeat timestamp to the current UTC time.
    /// </summary>
    public void UpdateHeartbeat()
    {
        LastHeartbeat = DateTimeOffset.UtcNow;
    }

    /// <summary>
    /// Increments the consecutive ping failures counter.
    /// Called when a ping attempt fails.
    /// </summary>
    public void IncrementPingFailures()
    {
        ConsecutivePingFailures++;
    }

    /// <summary>
    /// Resets the consecutive ping failures counter to zero.
    /// Called when a ping attempt succeeds.
    /// </summary>
    public void ResetPingFailures()
    {
        ConsecutivePingFailures = 0;
    }

    /// <summary>
    /// Marks the connection as failed with an error message.
    /// This should only be used for initialization failures.
    /// Connection remains in the active connections list but tools will throw exceptions when invoked.
    /// </summary>
    /// <param name="errorMessage">The error message describing why the connection failed</param>
    public void MarkAsFailed(string errorMessage)
    {
        Status = DataConnectorStatus.Failed;
        ErrorMessage = errorMessage;
        ConsecutivePingFailures++;
    }

    /// <summary>
    /// Marks the connection as disconnected with an error message.
    /// This is used when verification (ping) fails but the connection can potentially be reconnected.
    /// </summary>
    /// <param name="errorMessage">The error message describing why the connection was disconnected</param>
    public void MarkAsDisconnected(string errorMessage)
    {
        Status = DataConnectorStatus.Disconnected;
        ErrorMessage = errorMessage;
        ConsecutivePingFailures++;
    }

    /// <summary>
    /// Marks the connection as healthy/connected and clears any error message.
    /// This is used to restore a previously failed or disconnected connection.
    /// </summary>
    public void MarkAsConnected()
    {
        Status = DataConnectorStatus.Connected;
        ErrorMessage = null;
    }
}

/// <summary>
/// Metadata about an MCP connection for refresh/reconnection operations.
/// </summary>
public class McpConnectionMetadata
{
    public required string Type { get; init; }
    public string? Endpoint { get; init; }
    public string? Command { get; init; }
    public string[]? Arguments { get; init; }
    public string? WorkingDirectory { get; init; }

    /// <summary>
    /// Description of the connection purpose or functionality.
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Service type indicating the source and configuration requirements of the MCP server.
    /// This is a free-form string for categorization purposes only.
    /// </summary>
    public string? ServiceType { get; init; }

    /// <summary>
    /// Environment variables to pass to the MCP server process.
    /// </summary>
    public Dictionary<string, string>? EnvironmentVariables { get; init; }

    /// <summary>
    /// Azure Managed Identity resource ID for authentication.
    /// </summary>
    public string? Identity { get; init; }
}
