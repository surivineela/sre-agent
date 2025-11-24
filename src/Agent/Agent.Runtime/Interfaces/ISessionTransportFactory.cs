// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using ModelContextProtocol.Client;

namespace Agent.Runtime.Interfaces;

/// <summary>
/// Factory for creating session-based MCP client transports.
/// </summary>
public interface ISessionTransportFactory
{
    /// <summary>
    /// Creates a session websocket client transport for connecting to MCP servers via session pool.
    /// </summary>
    /// <param name="name">The name of the connection (will be sanitized)</param>
    /// <param name="command">The command to execute on the session pool</param>
    /// <param name="arguments">Arguments for the command</param>
    /// <returns>A configured client transport</returns>
    IClientTransport CreateSessionTransport(string name, string command, string[] arguments);
}
