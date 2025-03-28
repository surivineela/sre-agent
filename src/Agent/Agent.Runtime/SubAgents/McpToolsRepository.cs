// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.SubAgents;

// [Export]
public sealed class McpToolsRepository : IMcpConnectable
{
    private readonly Dictionary<string, AIFunction> _aiFunctions = new();
    private ConcurrentDictionary<McpConnection, IReadOnlyList<string>> _connectionToToolSignatures = new();

    private string GetAIFunctionSignature(
        McpConnection connection,
        AITool tool)
    {
        return $"{connection} {tool}";
    }

    public void TryAddServer(McpConnection connection)
    {
        List<string> toolSignatures = [];

        foreach (AIFunction tool in connection.Tools)
        {
            string sig = GetAIFunctionSignature(connection, tool);
            toolSignatures.Add(sig);
            _aiFunctions.TryAdd(sig, tool);
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
