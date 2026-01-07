using System.Net.WebSockets;
using System.Text;
using Agent.Common.ApiModels.Session;

namespace Session.Proxy.Services.McpProtocol;

/// <summary>
/// Protocol v2 handler: Messages prefixed with channel indicator ('1' for stdout, '2' for stderr).
/// </summary>
public class ProtocolHandlerV2 : IProtocolHandler
{
    private readonly ILogger<ProtocolHandlerV2> _logger;

    public int Version => 2;

    public ProtocolHandlerV2(ILogger<ProtocolHandlerV2> logger)
    {
        _logger = logger;
    }

    public async Task SendStdoutMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken)
    {
        // V2: Prefix with channel indicator for stdout
        var prefixedMessage = $"{McpProxyProtocol.ChannelStdout}{message}";
        var bytes = Encoding.UTF8.GetBytes(prefixedMessage);
        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }

    public async Task SendStderrMessageAsync(WebSocket webSocket, string message, CancellationToken cancellationToken)
    {
        // V2: Prefix with channel indicator for stderr and forward to client
        var prefixedMessage = $"{McpProxyProtocol.ChannelStderr}{message}";
        var bytes = Encoding.UTF8.GetBytes(prefixedMessage);
        await webSocket.SendAsync(
            new ArraySegment<byte>(bytes),
            WebSocketMessageType.Text,
            true,
            cancellationToken);
    }
}
