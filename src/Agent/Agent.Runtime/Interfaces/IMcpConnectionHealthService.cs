// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Runtime.Models;

namespace Agent.Runtime.Interfaces;

/// <summary>
/// Service for checking the health of MCP connections before tool invocation.
/// </summary>
public interface IMcpConnectionHealthService
{
    /// <summary>
    /// Validates that an MCP connection is healthy and can execute tools.
    /// </summary>
    /// <param name="connection">The MCP connection to validate</param>
    /// <param name="toolName">The name of the tool being invoked for error reporting</param>
    /// <exception cref="InvalidOperationException">Thrown when the connection is unhealthy and cannot execute tools</exception>
    void ValidateConnectionHealth(McpConnection connection, string toolName);

    /// <summary>
    /// Attempts to find an MCP connection by a tool signature.
    /// </summary>
    /// <param name="toolSignature">The tool signature (e.g., "connection_id_tool_name")</param>
    /// <returns>The MCP connection if found, null otherwise</returns>
    McpConnection? FindConnectionByToolSignature(string toolSignature);
}
