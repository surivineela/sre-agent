using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Session.Proxy.Services;

namespace Session.Proxy.Controllers;

/// <summary>
/// Controller for handling MCP proxy WebSocket connections.
/// </summary>
[ApiController]
[Route("/mcp")]
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
    /// </summary>
    /// <param name="cmd">The command to execute (e.g., npx)</param>
    /// <param name="args">JSON-encoded array of command arguments</param>
    /// <param name="cancellationToken">Cancellation token</param>
    [HttpGet]
    [Route("run")]
    public async Task HandleWebSocketConnection(
        [FromQuery] string cmd,
        [FromQuery] string? args,
        CancellationToken cancellationToken)
    {
        if (!HttpContext.WebSockets.IsWebSocketRequest)
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("WebSocket connection required");
            return;
        }

        if (string.IsNullOrEmpty(cmd))
        {
            HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
            await HttpContext.Response.WriteAsync("Missing 'cmd' query parameter");
            return;
        }

        string[] commandArgs = Array.Empty<string>();
        if (!string.IsNullOrEmpty(args))
        {
            try
            {
                commandArgs = JsonSerializer.Deserialize<string[]>(args) ?? Array.Empty<string>();
            }
            catch (JsonException)
            {
                HttpContext.Response.StatusCode = StatusCodes.Status400BadRequest;
                await HttpContext.Response.WriteAsync("Invalid 'args' query parameter - must be a JSON array");
                return;
            }
        }

        var webSocket = await HttpContext.WebSockets.AcceptWebSocketAsync();
        await _proxyService.HandleWebSocketConnection(webSocket, cmd, commandArgs, cancellationToken);
    }
}
