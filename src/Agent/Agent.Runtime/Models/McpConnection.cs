// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Interfaces;
using Agent.Runtime.SubAgents;
using ModelContextProtocol.Client;
using ModelContextProtocol.Configuration;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace Agent.Runtime.Models;

/// <summary>
/// Represents an active connection to an MCP server and the resources that are available on that server.
/// </summary>
public class McpConnection
{
    public required ILoggerFactory LoggerFactory { get; init; }
    public required IMcpConnectable Backend { get; init; }

    public string Id { get; private set; }
    public IList<AITool> Tools { get; private set; }
    public string? ServerInstructions { get; private set; }
    public IMcpClient Client { get; private set; }
    public string Url { get; private set; }

    private static Regex _unsafeToolNameChars = new Regex("[^a-zA-Z0-9_\\.\\-]", RegexOptions.Compiled);

    public McpConnection(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out Uri? _))
        {
            throw new ArgumentException("The provided string is not a valid URI.", url);
        }

        Url = url;
        Id = _unsafeToolNameChars.Replace(url, "");
    }

    public async Task Initialize()
    {
        var logger = LoggerFactory.CreateLogger($"{typeof(MCPMetaAgent).FullName!}.{Id}");

        try
        {

            McpClientOptions options = new()
            {
                ClientInfo = new() { Name = Id, Version = "1.0.0" }
            };

            var config = new McpServerConfig
            {
                Id = Id,
                Name = Id,
                TransportType = "sse",
                Location = Url
            };


            logger.LogInformation("Attempting to connect to {endpoint}", Url);

            Client = await McpClientFactory.CreateAsync(
                config,
                options,
                loggerFactory: LoggerFactory
            );


            // Can't use McpSessionScope yet because we need lower level functionality for pinging
            Tools = (await Client.GetAIFunctionsAsync()).ToList<AITool>();

            foreach (var tool in Tools)
            {
                logger.LogInformation("Imported tool: {tool} from MCP server {server}", tool.Name, config.Location);
            }

            if (!string.IsNullOrEmpty(Client.ServerInstructions))
            {
                ServerInstructions = Client.ServerInstructions;
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to initialize connection to {endpoint}", Url);
            throw;
        }
    }

    // Added ToString override
    public override string ToString()
    {
        return $"McpConnection: Id={Id}, Url={Url}";
    }
}
