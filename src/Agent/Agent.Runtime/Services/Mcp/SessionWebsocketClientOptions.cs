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
    /// Connection timeout in milliseconds (default: 30 seconds).
    /// </summary>
    public int ConnectionTimeoutMs { get; set; } = 30000;

    /// <summary>
    /// Optional Azure credential for authentication to session pool.
    /// </summary>
    public TokenCredential? Credential { get; set; }

    /// <summary>
    /// Session Identifier
    /// </summary>
    public string? SessionId { get; set; }
}
