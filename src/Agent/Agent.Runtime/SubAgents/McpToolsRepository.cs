// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;

namespace Agent.Runtime.SubAgents;

// [Export]
public class McpToolsRepository : IMcpConnectable
{
    private readonly ILoggerFactory _loggerFactory;

    private readonly Dictionary<string, AIFunction> _aiFunctions = new();
    private ConcurrentDictionary<McpConnection, IReadOnlyList<string>> _connectionToToolSignatures = new();

    public McpToolsRepository(
        ILoggerFactory loggerFactory)
    {
        _loggerFactory = loggerFactory;
    }

    public async Task InitializeAsync()
    {
        if (_connectionToToolSignatures.Count == 0)
        {
            McpConnection connection = new McpConnection(GetAzureMcpStdioConnection())
            {
                McpLoggerFactory = _loggerFactory,
                Backend = this
            };

            await connection.InitializeAsync();

            TryAddServer(connection);
        }
    }

    // TODO: Move to configuration
    private IClientTransport GetAzureMcpStdioConnection()
    {
        return new StdioClientTransport(new()
        {
            Name = "LocalAzureMcp",
            Command = "npx",
            Arguments = new string[]
            {
                "@azure/mcp@0.5.11",
                "server",
                "start"
            }
        });
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
