// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure.Core;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// Options for configuring a McpSessionWebsocketClientTransport.
/// </summary>
public class SessionWebsocketClientOptions
{
    /// <summary>
    /// The WebSocket server URL (e.g., "wss://session-pool/mcp/run").
    /// </summary>
    public required string ServerUrl { get; set; }

    /// <summary>
    /// The command to execute (e.g., "npx").
    /// </summary>
    public required string Command { get; set; }

    /// <summary>
    /// Arguments for the command.
    /// </summary>
    public required string[] Arguments { get; set; }

    /// <summary>
    /// Name for the transport (used for logging and identification).
    /// </summary>
    public string? Name { get; set; }

    /// <summary>
    /// Buffer size for WebSocket receive operations in bytes (default: 64KB).
    /// </summary>
    public int ReceiveBufferSize { get; set; } = 65536;

    /// <summary>
    /// Connection timeout in milliseconds (default: 120 seconds).
    /// Setup connection could be slow since it will acquire several AAD tokens.
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 120000;

    /// <summary>
    /// Optional Azure credential for authentication to session pool.
    /// </summary>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Session Identifier
    /// </summary>
    public string? SessionId { get; set; }

    /// <summary>
    /// Environment variables to pass to the MCP server process.
    /// </summary>
    public Dictionary<string, string>? EnvVars { get; set; }

    /// <summary>
    /// Token scopes required by some Microsoft-provided MCP servers.
    /// These scopes will be used to acquire tokens that are made available to the MCP server via MSI endpoint.
    /// </summary>
    public string[]? TokenScopes { get; set; }

    /// <summary>
    /// Optional Azure credential for agent identity to acquire action tokens.
    /// This is separate from the session pool credential and represents the agent's identity
    /// for accessing Azure resources (same as data connector credentials).
    /// Only used for stdio MCP servers, not HTTP servers.
    /// </summary>
    public TokenCredential? DataConnectorCredential { get; set; }
}
