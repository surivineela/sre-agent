// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Agent.Runtime.Services.Mcp;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.SubAgents;

public class McpToolsRepository : IMcpConnectable
{
    private readonly ILogger<McpToolsRepository> _logger;
    private readonly IServiceProvider _serviceProvider;

    private readonly ConcurrentDictionary<string, AIFunction> _aiFunctions = new();
    private readonly ConcurrentDictionary<McpConnection, IReadOnlyList<string>> _connectionToToolSignatures = new();

    public McpToolsRepository(
        ILogger<McpToolsRepository> logger,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
    }


    private string GetMcpToolSignature(
        McpConnection connection,
        AITool tool)
    {
        // Always prefix MCP tools with connection ID for unique namespacing
        // Format: {connectionId}_{toolName}
        return $"{connection.Id}_{tool.Name}";
    }

    public void TryAddServer(McpConnection connection)
    {
        List<string> toolSignatures = [];

        if (connection.Tools != null)
        {
            // Get the health service from DI container
            var healthService = _serviceProvider.GetService<IMcpConnectionHealthService>();

            // Agent Builder is responsible for selecting which tools each agent should use.
            var toolsToAdd = connection.Tools.ToList();

            foreach (AIFunction tool in toolsToAdd)
            {
                string sig = GetMcpToolSignature(connection, tool);
                toolSignatures.Add(sig);

                // Wrap the tool with a renamed version that uses the prefixed signature
                // but delegates to the original tool for execution with health checking
                var mcpTool = new McpToolAIFunction(sig, tool, healthService);
                _aiFunctions.TryAdd(sig, mcpTool);
            }

            _logger.LogInternalInformation(
                    "Added {Count} tools from MCP connection '{ConnectionId}'",
                    toolsToAdd.Count,
                    connection.Id);
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
                _aiFunctions.TryRemove(sig, out _);
            }
        }
    }

    public List<AIFunction> GetAllFunctions()
    {
        return _aiFunctions.Values.ToList();
    }
}
