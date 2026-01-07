// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Agent.Common.ApiModels.Session;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Protocol;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// WebSocket-based transport implementation for MCP protocol.
/// Implements ITransport interface for message sending/receiving operations.
/// </summary>
public class SessionWebsocketTransport : ITransport, IAsyncDisposable
{
    private readonly ILogger _logger;
    private readonly string _name;
    private readonly int _receiveBufferSize;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private int _disposed; // 0 = not disposed, 1 = disposed (use Interlocked)
    private readonly Channel<JsonRpcMessage> _messageChannel = Channel.CreateUnbounded<JsonRpcMessage>();

    /// <summary>
    /// Callback invoked when the WebSocket connection is lost or closed unexpectedly.
    /// </summary>
    public Action<string>? OnDisconnected { get; set; }

    /// <summary>
    /// Callback invoked when stderr data is received from the MCP server.
    /// </summary>
    public Action<string>? OnStderrReceived { get; set; }

    /// <summary>
    /// Gets the name of the transport.
    /// </summary>
    public string Name => _name;

    /// <summary>
    /// Gets the session ID (not used in WebSocket session pool transport).
    /// </summary>
    public string? SessionId => null;

    /// <summary>
    /// Gets the message reader for consuming received messages.
    /// </summary>
    public ChannelReader<JsonRpcMessage> MessageReader => _messageChannel.Reader;

    /// <summary>
    /// Initializes a new instance of the McpSessionWebsocketTransport class.
    /// </summary>
    /// <param name="webSocket">The connected WebSocket instance</param>
    /// <param name="name">Name for the transport (used for logging and identification)</param>
    /// <param name="receiveBufferSize">Buffer size for WebSocket receive operations in bytes</param>
    /// <param name="logger">Logger instance</param>
    internal SessionWebsocketTransport(
        ClientWebSocket webSocket,
        string name,
        int receiveBufferSize,
        ILogger logger)
    {
        _webSocket = webSocket ?? throw new ArgumentNullException(nameof(webSocket));
        _name = name ?? throw new ArgumentNullException(nameof(name));
        _receiveBufferSize = receiveBufferSize;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _cts = new CancellationTokenSource();

        // Start the receive loop immediately so MessageReader can start providing messages
        _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);
        _logger.LogInternalDebug("WebSocket receive loop started");
    }

    /// <summary>
    /// Sends a message through the WebSocket transport (MCP protocol format).
    /// </summary>
    /// <param name="message">The JSON-RPC message to send</param>
    /// <param name="cancellationToken">Token to cancel the send operation</param>
    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(message, new JsonSerializerOptions
        {
            DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
        });

        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var displayMessage = json.Length > 200 ? json[..200] + "..." : json;
            _logger.LogInternalDebug("Sending message: {Message}", displayMessage);
        }

        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            _logger.LogInternalError("Attempted to send message on closed WebSocket");
            OnDisconnected?.Invoke("WebSocket not open when attempting to send message");
            throw new InvalidOperationException("Transport is not connected");
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(json);
            await _webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken);
        }
        finally
        {
            _sendLock.Release();
        }
    }

    /// <summary>
    /// Receives a single message from the WebSocket.
    /// </summary>
    private async Task<(int Channel, string? Message)?> ReceiveMessageAsync(CancellationToken cancellationToken)
    {
        var webSocket = _webSocket;
        if (webSocket == null)
        {
            return null;
        }

        // Allow reading in Open or CloseReceived states to drain buffered messages
        // CloseReceived means we received a close frame, but there may be pending data messages before it
        if (webSocket.State != WebSocketState.Open && webSocket.State != WebSocketState.CloseReceived)
        {
            return null;
        }

        var buffer = new byte[_receiveBufferSize];
        using var ms = new MemoryStream();

        WebSocketReceiveResult result;
        try
        {
            do
            {
                result = await webSocket.ReceiveAsync(new ArraySegment<byte>(buffer), cancellationToken);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                ms.Write(buffer, 0, result.Count);
            }
            while (!result.EndOfMessage);
        }
        catch (WebSocketException)
        {
            // WebSocket was closed between state check and ReceiveAsync (TOCTOU race)
            return null;
        }
        catch (ObjectDisposedException)
        {
            // WebSocket was disposed between state check and ReceiveAsync (TOCTOU race)
            return null;
        }

        ms.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(ms, Encoding.UTF8);
        var fullMessage = await reader.ReadToEndAsync(cancellationToken);

        if (string.IsNullOrEmpty(fullMessage))
            return null;

        // Parse channel indicator (first byte: '1' for stdout, '2' for stderr)
        // Handshake messages (like "ok") are not prefixed and are treated as stdout
        if (fullMessage.Length < 2)
        {
            // Too short to have channel indicator - treat as handshake/unprefixed message (stdout)
            return (McpProxyProtocol.ChannelStdoutValue, fullMessage);
        }

        char channelChar = fullMessage[0];
        if (channelChar == McpProxyProtocol.ChannelStdout || channelChar == McpProxyProtocol.ChannelStderr)
        {
            // Valid channel indicator found
            int channel = channelChar - '0';  // '1' -> 1, '2' -> 2
            string message = fullMessage.Substring(1);
            return (channel, message);
        }

        // No valid channel indicator - treat as unprefixed message (stdout, e.g., handshake)
        return (McpProxyProtocol.ChannelStdoutValue, fullMessage);
    }

    /// <summary>
    /// Continuously receives messages from the WebSocket.
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Keep reading until ReceiveMessageAsync returns null (connection closed or EOF)
            // Don't check WebSocket.State here - let ReceiveMessageAsync handle it
            // This ensures we drain all buffered messages before exiting
            while (!cancellationToken.IsCancellationRequested)
            {
                var result = await ReceiveMessageAsync(cancellationToken);
                if (result.HasValue)
                {
                    var (channel, message) = result.Value;

                    if (message == null)
                        continue;

                    // Handle channel 2 (stderr)
                    if (channel == McpProxyProtocol.ChannelStderrValue)
                    {
                        _logger.LogInternalWarning("MCP stderr: {Stderr}", message);

                        // Invoke callback if configured
                        OnStderrReceived?.Invoke(message);

                        continue;  // Don't parse as JSON-RPC
                    }

                    // Handle channel 1 (stdout) - JSON-RPC parsing
                    if (_logger.IsEnabled(LogLevel.Debug))
                    {
                        var displayMessage = message.Length > 200 ? message[..200] + "..." : message;
                        _logger.LogInternalDebug("Received message: {Message}", displayMessage);
                    }

                    // Try to parse as JSON-RPC message and write to channel
                    try
                    {
                        var jsonRpcMessage = JsonSerializer.Deserialize<JsonRpcMessage>(message);
                        if (jsonRpcMessage != null)
                        {
                            _logger.LogInternalDebug("Writing message to channel");
                            await _messageChannel.Writer.WriteAsync(jsonRpcMessage, cancellationToken);
                        }
                    }
                    catch (JsonException ex)
                    {
                        _logger.LogInternalWarning(ex, "Failed to parse message as JSON-RPC: {Message}", message);
                    }
                }
                else
                {
                    _logger.LogInternalInformation("Connection closed by server");
                    OnDisconnected?.Invoke("Connection closed by server");
                    break;
                }
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInternalDebug("Receive loop cancelled");
        }
        catch (WebSocketException ex) when (ex.WebSocketErrorCode == WebSocketError.ConnectionClosedPrematurely)
        {
            _logger.LogInternalWarning("Connection closed prematurely");
            OnDisconnected?.Invoke("WebSocket connection closed prematurely");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in receive loop");
            OnDisconnected?.Invoke($"WebSocket error: {ex.Message}");
        }
        finally
        {
            _messageChannel.Writer.Complete();
        }
    }

    /// <summary>
    /// Disposes the transport asynchronously.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            return; // Already disposed
        }

        _logger.LogInternalInformation("Disposing McpSessionWebsocketTransport: {Name}", Name);

        // Cancel the receive loop first
        _cts?.Cancel();

        // Close WebSocket gracefully with timeout
        if (_webSocket?.State == WebSocketState.Open)
        {
            try
            {
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(5));
                await _webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Disposing", cts.Token);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "WebSocket close failed during disposal");
            }
        }

        // Wait for receive task to complete
        if (_receiveTask != null)
        {
            try
            {
                await _receiveTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Receive task cleanup failed");
            }
        }

        // Dispose all resources
        _webSocket?.Dispose();
        _cts?.Dispose();
        _sendLock.Dispose();
    }
}
