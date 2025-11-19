// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Azure.Core;
using Microsoft.Extensions.Logging;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// WebSocket-based MCP client transport for connecting to session pool MCP proxy servers.
/// This transport connects to a WebSocket endpoint that launches and proxies MCP servers.
/// Implements IClientTransport interface for connection establishment.
/// </summary>
public class SessionWebsocketClientTransport : IClientTransport
{
    private readonly ILogger _logger;
    private readonly SessionWebsocketClientOptions _options;

    /// <summary>
    /// Gets the name of the transport.
    /// </summary>
    public string Name => _options.Name ?? $"SessionPool-{_options.Command}";

    /// <summary>
    /// Initializes a new instance of the McpSessionWebsocketClientTransport class.
    /// </summary>
    /// <param name="options">Configuration options for the transport</param>
    /// <param name="logger">Logger instance</param>
    public SessionWebsocketClientTransport(
        SessionWebsocketClientOptions options,
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
    /// Connects to the WebSocket server and returns an ITransport for communication.
    /// This method is called by the MCP SDK and must return an ITransport instance.
    /// </summary>
    public async Task<ITransport> ConnectAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Connecting McpSessionWebsocketClientTransport: {Name}", Name);

        using var timeoutCts = new CancellationTokenSource(_options.ConnectionTimeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);

        ClientWebSocket? webSocket = null;
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

            webSocket = new ClientWebSocket();
            webSocket.Options.CollectHttpResponseDetails = true;

            // Add authorization header if token is available
            if (token != null && _options.SessionId != null)
            {
                webSocket.Options.SetRequestHeader("Authorization", $"Bearer {token}");
                fullUrl += $"&identifier={_options.SessionId}";
                _logger.LogInternalDebug("Added session identifier: {Identifier}", _options.SessionId);
            }

            _logger.LogInternalDebug("Connecting to: {Url}", fullUrl);
            await webSocket.ConnectAsync(new Uri(fullUrl), linkedCts.Token);

            // Check HTTP status code from the WebSocket handshake
            var statusCode = webSocket.HttpStatusCode;
            if (statusCode != System.Net.HttpStatusCode.SwitchingProtocols)
            {
                throw new InvalidOperationException("WebSocket handshake failed. Status code: " + statusCode);
            }
            _logger.LogInternalInformation("WebSocket connected successfully");

            // Read the first message (should be "ok" or error)
            var firstMessage = await ReceiveHandshakeMessageAsync(webSocket, _options.ReceiveBufferSize, linkedCts.Token);
            if (firstMessage == null)
            {
                throw new InvalidOperationException("Connection closed by server immediately after connecting.");
            }

            _logger.LogInternalDebug("Server handshake response: {Response}", firstMessage);

            if (!firstMessage.Equals("ok", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"Server returned error during handshake: {firstMessage}");
            }

            _logger.LogInternalInformation("McpSessionWebsocketClientTransport connected and ready");

            // Create and return the transport instance
            var transport = new SessionWebsocketTransport(
                webSocket,
                Name,
                _options.ReceiveBufferSize,
                _logger);

            return transport;
        }
        catch (OperationCanceledException ex) when (timeoutCts.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogInternalError("Connection timeout after {TimeoutMs}ms: {ExceptionMessage}", _options.ConnectionTimeoutMs, ex.Message);
            webSocket?.Dispose();
            throw new TimeoutException($"Connection timeout after {_options.ConnectionTimeoutMs}ms");
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to connect McpSessionWebsocketClientTransport");
            webSocket?.Dispose();
            throw;
        }
    }

    /// <summary>
    /// Receives the handshake message from the WebSocket server.
    /// </summary>
    private static async Task<string?> ReceiveHandshakeMessageAsync(
        ClientWebSocket webSocket,
        int receiveBufferSize,
        CancellationToken cancellationToken)
    {
        if (webSocket.State != WebSocketState.Open)
        {
            return null;
        }

        var buffer = new byte[receiveBufferSize];
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
}
