using Agent.Runtime.Interfaces;
using Agent.Runtime.SubAgents;
using McpDotNet.Client;
using McpDotNet.Configuration;
using McpDotNet.Extensions.AI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
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
    public string? SystemInstructions { get; private set; }
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

            var factory = new McpClientFactory(
                [config],
                options,
                NullLoggerFactory.Instance
            );

            logger.LogInformation("Attempting to connect to {endpoint}", Url);

            // Can't use McpSessionScope yet because we need lower level functionality for pinging
            Client = await factory.GetClientAsync(Id);
            var tools = await Client.ListToolsAsync();
            Tools = tools.Tools.Select(t => t.ToAITool(Client)).ToList();

            if (!string.IsNullOrEmpty(Client.ServerInstructions))
            {
                SystemInstructions = Client.ServerInstructions;
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