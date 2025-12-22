using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Agent.Core.Models.Session;
using Microsoft.AspNetCore.Mvc;
using Session.Identity.Attributes;
using Session.Proxy.Services;

namespace Session.Proxy.Controllers;

/// <summary>
/// Controller for handling MCP proxy WebSocket connections.
/// </summary>
[ApiController]
[Route("/mcp")]
[SessionMode(SessionMode.Proxy)]
public class McpProxyController : ControllerBase
{
    private readonly McpProxyService _proxyService;
    private readonly ILogger<McpProxyController> _logger;

    public McpProxyController(McpProxyService proxyService, ILogger<McpProxyController> logger)
    {
        _proxyService = proxyService;
        _logger = logger;
    }

    /// <summary>
    /// Handles WebSocket connections for MCP proxy.
    /// After accepting the WebSocket connection, the client must send a JSON message
    /// with the connection parameters (command, arguments, environment variables, etc.).
    /// The server will validate the parameters and respond with "ok" or an error message.
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet]
    [Route("run")]
    public async Task HandleWebSocketConnection(CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("WebSocket connection required");
            return;
        }

        var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();

        try
        {
            // Receive the first message containing connection parameters
            var connectionRequest = await ReceiveConnectionRequestAsync(webSocket, cancellationToken);

            if (connectionRequest == null)
            {
                await SendErrorAndCloseAsync(webSocket, "Connection closed before receiving initialization message", cancellationToken);
                return;
            }

            // Validate the connection request
            var validationError = ValidateConnectionRequest(connectionRequest);
            if (validationError != null)
            {
                await SendErrorAndCloseAsync(webSocket, validationError, cancellationToken);
                return;
            }

            // Cleanup Ev2 bits if non-first-party client
            if (connectionRequest.IsFirstParty != true)
            {
                _proxyService.CleanupInternalBits();
            }

            // Get protocol version (default to 1 if not specified)
            var protocolVersion = connectionRequest.ProtocolVersion ?? McpConnectionRequest.DefaultProtocolVersion;

            // Parameters are valid, proceed with the connection
            await _proxyService.HandleWebSocketConnection(
                webSocket,
                connectionRequest.Command,
                connectionRequest.Arguments,
                connectionRequest.EnvironmentVariables,
                connectionRequest.ActionTokens,
                protocolVersion,
                cancellationToken);
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Invalid JSON in connection request");
            await SendErrorAndCloseAsync(webSocket, $"Invalid JSON format: {ex.Message}", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling WebSocket connection");
            await SendErrorAndCloseAsync(webSocket, $"Error: {ex.Message}", cancellationToken);
        }
    }

    /// <summary>
    /// Receives and deserializes the connection request from the WebSocket.
    /// </summary>
    private async Task<McpConnectionRequest?> ReceiveConnectionRequestAsync(
        WebSocket webSocket,
        CancellationToken cancellationToken)
    {
        var buffer = new byte[8192];
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
        var json = await reader.ReadToEndAsync(cancellationToken);

        _logger.LogDebug("Received connection request: {Json}", json);

        return JsonSerializer.Deserialize<McpConnectionRequest>(json);
    }

    /// <summary>
    /// Validates the connection request and returns an error message if invalid.
    /// </summary>
    private string? ValidateConnectionRequest(McpConnectionRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Command))
        {
            return "Missing 'cmd' field in connection request";
        }

        if (request.Arguments == null)
        {
            return "Missing 'args' field in connection request";
        }

        // Validate protocol version
        var protocolVersion = request.ProtocolVersion ?? McpConnectionRequest.DefaultProtocolVersion;
        if (protocolVersion < 1 || protocolVersion > McpConnectionRequest.MaxSupportedProtocolVersion)
        {
            return $"Unsupported protocol version {protocolVersion}. Server supports versions 1-{McpConnectionRequest.MaxSupportedProtocolVersion}.";
        }

        return null;
    }

    /// <summary>
    /// Sends an error message to the client and closes the WebSocket connection.
    /// </summary>
    private async Task SendErrorAndCloseAsync(
        WebSocket webSocket,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        _logger.LogWarning("Sending error to client: {Error}", errorMessage);

        try
        {
            var bytes = Encoding.UTF8.GetBytes(errorMessage);
            await webSocket.SendAsync(
                new ArraySegment<byte>(bytes),
                WebSocketMessageType.Text,
                true,
                cancellationToken);

            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.PolicyViolation,
                    "Invalid connection request",
                    cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error sending error message to client");
        }
    }
}
