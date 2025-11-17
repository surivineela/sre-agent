// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using Agent.Logging;
using Azure.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// Options for configuring a McpSessionWebsocketClient.
/// </summary>
public class McpSessionWebsocketClientOptions
{
    /// <summary>
    /// The WebSocket server URL (e.g., "wss://session-pool/run").
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
    /// Optional Azure credential for authentication.
    /// </summary>
    public TokenCredential? Credential { get; set; }

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
}

/// <summary>
/// WebSocket-based MCP client transport for connecting to session pool MCP proxy servers.
/// This transport connects to a WebSocket endpoint that launches and proxies MCP servers.
/// </summary>
public class McpSessionWebsocketClient : IClientTransport, ITransport
{
    private readonly ILogger _logger;
    private readonly McpSessionWebsocketClientOptions _options;
    private ClientWebSocket? _webSocket;
    private CancellationTokenSource? _cts;
    private Task? _receiveTask;
    private readonly SemaphoreSlim _sendLock = new(1, 1);
    private readonly SemaphoreSlim _disposeLock = new(1, 1);
    private int _disposed; // 0 = not disposed, 1 = disposed (use Interlocked)
    private bool _isConnected;
    private Action<string>? _onMessage;
    private readonly Channel<JsonRpcMessage> _messageChannel = Channel.CreateUnbounded<JsonRpcMessage>();

    /// <summary>
    /// Gets the name of the transport.
    /// </summary>
    public string Name => _options.Name ?? $"SessionPool-{_options.Command}";

    /// <summary>
    /// Gets the session ID (not used in WebSocket session pool transport).
    /// </summary>
    public string? SessionId => null;

    /// <summary>
    /// Gets the message reader for consuming received messages.
    /// </summary>
    public ChannelReader<JsonRpcMessage> MessageReader => _messageChannel.Reader;

    /// <summary>
    /// Initializes a new instance of the McpSessionWebsocketClient class.
    /// </summary>
    /// <param name="options">Configuration options for the transport</param>
    /// <param name="logger">Logger instance</param>
    public McpSessionWebsocketClient(
        McpSessionWebsocketClientOptions options,
        ILogger logger)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (string.IsNullOrEmpty(_options.ServerUrl))
            throw new ArgumentException("ServerUrl is required", nameof(options));
        if (string.IsNullOrEmpty(_options.Command))
            throw new ArgumentException("Command is required", nameof(options));
        if (_options.Arguments == null)
            throw new ArgumentException("Arguments is required", nameof(options));
    }

    /// <summary>
    /// Connects to the WebSocket server (required by IClientTransport).
    /// This method is called by the MCP SDK and must return an ITransport (this instance).
    /// </summary>
    public async Task<ITransport> ConnectAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected)
        {
            throw new InvalidOperationException("Transport is already connected");
        }

        _logger.LogInternalInformation("Connecting McpSessionWebsocketClient: {Name}", Name);

        using var timeoutCts = new CancellationTokenSource(_options.ConnectionTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        try
        {
            // Get Azure token if credential is provided
            string? token = null;
            if (_options.Credential != null)
            {
                _logger.LogInternalDebug("Acquiring Azure token for scope: https://dynamicsessions.io");
                var tokenRequestContext = new TokenRequestContext(new[] { "https://dynamicsessions.io/.default" });
                var tokenResult = await _options.Credential.GetTokenAsync(tokenRequestContext, linkedCts.Token);
                token = tokenResult.Token;
                _logger.LogInternalDebug("Azure token acquired successfully");
            }

            // Build the WebSocket URL with query parameters
            var argsJson = JsonSerializer.Serialize(_options.Arguments);
            var encodedArgs = Uri.EscapeDataString(argsJson);
            var encodedCmd = Uri.EscapeDataString(_options.Command);
            var fullUrl = $"{_options.ServerUrl}?cmd={encodedCmd}&args={encodedArgs}";

            _webSocket = new ClientWebSocket();
            _cts = new CancellationTokenSource();

            // Add authorization header if token is available
            if (token != null)
            {
                _webSocket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
                var identifier = Guid.NewGuid().ToString();
                fullUrl += $"&identifier={identifier}";
                _logger.LogInternalDebug("Added session identifier: {Identifier}", identifier);
            }

            _logger.LogInternalDebug("Connecting to: {Url}", fullUrl);
            await _webSocket.ConnectAsync(new Uri(fullUrl), linkedCts.Token);
            _logger.LogInternalInformation("WebSocket connected successfully");

            // Read the first message (should be "ok" or error)
            var firstMessage = await ReceiveMessageAsync(_cts.Token);
            _logger.LogInternalDebug("Server handshake response: {Response}", firstMessage);

            if (firstMessage != "ok")
            {
                throw new InvalidOperationException($"Server returned error during handshake: {firstMessage}");
            }

            _isConnected = true;

            // Start the receive loop
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_cts.Token), _cts.Token);

            _logger.LogInternalInformation("McpSessionWebsocketClient connected and ready");

            // Return this instance as the ITransport implementation
            return this;
        }
        catch (OperationCanceledException) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInternalError("Connection timeout after {TimeoutMs}ms", _options.ConnectionTimeoutMs);
            await CleanupAsync();
            throw new TimeoutException($"Connection timeout after {_options.ConnectionTimeoutMs}ms");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to connect McpSessionWebsocketClient");
            await CleanupAsync();
            throw;
        }
    }

    /// <summary>
    /// Starts the transport and begins listening for messages.
    /// This is called by the MCP SDK when creating the client.
    /// Note: The actual connection is established in ConnectAsync, this just registers the callback.
    /// </summary>
    /// <param name="onMessage">Callback to invoke when a message is received</param>
    /// <param name="cancellationToken">Cancellation token</param>
    public Task StartAsync(Action<string> onMessage, CancellationToken cancellationToken = default)
    {
        if (!_isConnected)
        {
            throw new InvalidOperationException("Transport is not connected. Call ConnectAsync first.");
        }

        _onMessage = onMessage ?? throw new ArgumentNullException(nameof(onMessage));
        _logger.LogInternalDebug("StartAsync: Message callback registered");

        return Task.CompletedTask;
    }

    /// <summary>
    /// Sends a message through the WebSocket transport (MCP protocol format).
    /// </summary>
    /// <param name="message">The JSON-RPC message to send</param>
    /// <param name="cancellationToken">Token to cancel the send operation</param>
    public async Task SendMessageAsync(JsonRpcMessage message, CancellationToken cancellationToken = default)
    {
        var json = JsonSerializer.Serialize(message);
        await SendAsync(json, cancellationToken);
    }

    /// <summary>
    /// Sends a raw string message through the WebSocket transport.
    /// </summary>
    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (_logger.IsEnabled(LogLevel.Debug))
        {
            var displayMessage = message.Length > 200 ? message[..200] + "..." : message;
            _logger.LogInternalDebug("Sending message: {Message}", displayMessage);
        }

        if (_webSocket == null || _webSocket.State != WebSocketState.Open)
        {
            throw new InvalidOperationException("Transport is not connected");
        }

        await _sendLock.WaitAsync(cancellationToken);
        try
        {
            var bytes = Encoding.UTF8.GetBytes(message);
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
    /// Closes the transport connection.
    /// </summary>
    public async Task CloseAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Closing McpSessionWebsocketClient: {Name}", Name);

        if (_webSocket != null && _webSocket.State == WebSocketState.Open)
        {
            try
            {
                await _webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure,
                    "Client closing",
                    cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Error during WebSocket close");
            }
        }

        await CleanupAsync();
    }

    /// <summary>
    /// Receives a single message from the WebSocket.
    /// </summary>
    private async Task<string?> ReceiveMessageAsync(CancellationToken cancellationToken)
    {
        var webSocket = _webSocket;
        if (webSocket == null || webSocket.State != WebSocketState.Open)
        {
            return null;
        }

        var buffer = new byte[_options.ReceiveBufferSize];
        using var ms = new MemoryStream();

        WebSocketReceiveResult result;
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

        ms.Seek(0, SeekOrigin.Begin);
        using var reader = new StreamReader(ms, Encoding.UTF8);
        return await reader.ReadToEndAsync(cancellationToken);
    }

    /// <summary>
    /// Continuously receives messages from the WebSocket.
    /// </summary>
    private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
    {
        try
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                var webSocket = _webSocket;
                if (webSocket == null || webSocket.State != WebSocketState.Open)
                {
                    break;
                }

                var message = await ReceiveMessageAsync(cancellationToken);
                if (message != null)
                {
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

                    // Also invoke the callback if set (for backward compatibility)
                    _onMessage?.Invoke(message);
                }
                else
                {
                    _logger.LogInternalInformation("Connection closed by server");
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
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error in receive loop");
        }
        finally
        {
            _messageChannel.Writer.Complete();
        }
    }

    /// <summary>
    /// Cleans up resources.
    /// </summary>
    private async Task CleanupAsync()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null;
        }

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
                _logger.LogInternalWarning(ex, "Error waiting for receive task to complete");
            }
            _receiveTask = null;
        }

        if (_webSocket != null)
        {
            _webSocket.Dispose();
            _webSocket = null;
        }
    }

    /// <summary>
    /// Disposes the transport.
    /// </summary>
    public void Dispose()
    {
        // Atomically check and set disposed flag (0 -> 1)
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            // Already disposed
            return;
        }

        // We successfully claimed disposal ownership
        _disposeLock.Wait();
        try
        {
            // Cancel operations first
            _cts?.Cancel();

            // Close websocket synchronously
            if (_webSocket != null && _webSocket.State == WebSocketState.Open)
            {
                try
                {
                    _webSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "Client closing", CancellationToken.None)
                        .ConfigureAwait(false)
                        .GetAwaiter()
                        .GetResult();
                }
                catch (Exception ex)
                {
                    _logger.LogInternalWarning(ex, "Error during synchronous close");
                }
            }

            // Clean up resources synchronously
            _webSocket?.Dispose();
            _cts?.Dispose();
        }
        finally
        {
            _disposeLock.Release();
        }

        // Dispose locks AFTER releasing them
        _sendLock.Dispose();
        _disposeLock.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        // Atomically check and set disposed flag (0 -> 1)
        if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0)
        {
            // Already disposed
            return;
        }

        // We successfully claimed disposal ownership
        await _disposeLock.WaitAsync();
        try
        {
            await CloseAsync();
        }
        finally
        {
            _disposeLock.Release();
        }

        // Dispose locks AFTER releasing them
        _sendLock.Dispose();
        _disposeLock.Dispose();
    }
}
