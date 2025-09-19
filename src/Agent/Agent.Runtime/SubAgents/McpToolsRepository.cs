// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Configuration;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;

namespace Agent.Runtime.SubAgents;

// [Export]
public class McpToolsRepository : IMcpConnectable
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly MCPSettings _mcpSettings;

    private readonly Dictionary<string, AIFunction> _aiFunctions = new();
    private ConcurrentDictionary<McpConnection, IReadOnlyList<string>> _connectionToToolSignatures = new();

    public McpToolsRepository(
        ILoggerFactory loggerFactory,
        IOptions<MCPSettings> mcpSettings)
    {
        _loggerFactory = loggerFactory;
        _mcpSettings = mcpSettings.Value;
    }

    public async Task InitializeAsync()
    {
        if (_connectionToToolSignatures.Count == 0)
        {
            // Initialize STDIO connections from configuration
            foreach (var stdioConfig in _mcpSettings.StdioConnections.Where(c => c.Enabled))
            {
                var transport = new StdioClientTransport(new()
                {
                    Name = stdioConfig.Name,
                    Command = stdioConfig.Command,
                    Arguments = stdioConfig.Arguments
                });

                var connection = new McpConnection(transport)
                {
                    McpLoggerFactory = _loggerFactory,
                    Backend = this
                };

                await connection.InitializeAsync();
                TryAddServer(connection);
            }
        }
    }

    private string GetAIFunctionSignature(
        McpConnection connection,
        AITool tool)
    {
        return $"{connection} {tool}";
    }

    public void TryAddServer(McpConnection connection)
    {
        List<string> toolSignatures = [];

        if (connection.Tools != null)
        {
            foreach (AIFunction tool in connection.Tools)
            {
                string sig = GetAIFunctionSignature(connection, tool);
                toolSignatures.Add(sig);
                _aiFunctions.TryAdd(sig, tool);
            }
        }

        _connectionToToolSignatures.TryAdd(connection, toolSignatures.AsReadOnly());
    }

    /// <inheritdoc />
    public void TryRemoveServer(McpConnection connection)
    {
        if (_connectionToToolSignatures.TryRemove(connection, out IReadOnlyList<string>? toolSignatures))
        {
            foreach (string sig in toolSignatures)
            {
                _aiFunctions.Remove(sig);
            }
        }
    }

    public List<AIFunction> GetAllFunctions()
    {
        return _aiFunctions.Values.ToList();
    }
}
