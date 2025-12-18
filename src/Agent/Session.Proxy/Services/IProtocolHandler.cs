using System.Net.WebSockets;

namespace Session.Proxy.Services;

/// <summary>
/// Handles protocol-specific message formatting and transmission.
/// </summary>
public interface IProtocolHandler
{
    /// <summary>
    /// Protocol version this handler supports.
    /// </summary>
    int Version { get; }

    /// <summary>
    /// Formats and sends an stdout message from the MCP process to the WebSocket.
    /// </summary>
    Task SendStdoutMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken);

    /// <summary>
    /// Formats and sends an stderr message from the MCP process to the WebSocket.
    /// For v1, this may be a no-op (stderr not forwarded).
    /// </summary>
    Task SendStderrMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken);
}
