using System.Net.WebSockets;
using System.Text;

namespace Session.Proxy.Services.McpProtocol;

/// <summary>
/// Protocol v1 handler: Plain text messages, no channel indicators, stderr not forwarded.
/// </summary>
public class ProtocolHandlerV1 : IProtocolHandler
{
    private readonly ILogger<ProtocolHandlerV1> _logger;

    public int Version => 1;

    public ProtocolHandlerV1(ILogger<ProtocolHandlerV1> logger)
    {
        _logger = logger;
    }

    public async Task SendStdoutMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken)
    {
        // V1: Send message as-is, no prefix
        var bytes = Encoding.UTF8.GetBytes(message);
        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    public Task SendStderrMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken)
    {
        // V1: Stderr not forwarded to client, only logged
        // (logging happens in McpProxyService)
        return Task.CompletedTask;
    }
}
