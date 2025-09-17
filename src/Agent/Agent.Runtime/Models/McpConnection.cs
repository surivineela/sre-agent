// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.RegularExpressions;
using Agent.Runtime.Interfaces;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Agent.Runtime.Models;

/// <summary>
/// Represents an active connection to an MCP server and the resources that are available on that server.
/// </summary>
public class McpConnection
{
    public required ILoggerFactory McpLoggerFactory { get; init; }
    public required IMcpConnectable Backend { get; init; }

    public string Id { get; private set; }
    public IList<AITool>? Tools { get; private set; }
    public string? ServerInstructions { get; private set; }
    public IMcpClient? Client { get; private set; }
    public IClientTransport ClientTransport { get; private set; }

    private bool _initialized = false;
    private static Regex _unsafeToolNameChars = new Regex("[^a-zA-Z0-9_\\.\\-]", RegexOptions.Compiled);

    public McpConnection(IClientTransport clientTransport)
    {
        if (clientTransport == null)
        {
            throw new ArgumentException("The provided client transport is null.", nameof(clientTransport));
        }

        Id = _unsafeToolNameChars.Replace(clientTransport.Name, "");
        ClientTransport = clientTransport;
    }

    public async Task InitializeAsync()
    {
        var logger = McpLoggerFactory.CreateLogger($"{typeof(MCPMetaAgent).FullName!}.{Id}");

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

            logger.LogInternalInformation("Attempting to connect to {endpoint}", Id);

            Client = await McpClientFactory.CreateAsync(
                ClientTransport,
                options,
                loggerFactory: loggerFactory
            );

            Tools = (await Client.ListToolsAsync()).ToList<AITool>();

            foreach (var tool in Tools)
            {
                logger.LogInternalInformation("Imported tool: {tool} from MCP server {server}", tool.Name, Id);
            }

            if (!string.IsNullOrEmpty(Client.ServerInstructions))
            {
                ServerInstructions = Client.ServerInstructions;
            }

            _initialized = true;
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to initialize connection to {endpoint}", Id);
            throw;
        }
    }

    // Added ToString override
    public override string ToString()
    {
        return $"McpConnection: Id={Id}";
    }
}
