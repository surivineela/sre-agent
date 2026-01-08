// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Configuration;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Models;
using Agent.Web.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Client;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/mcp/connections")]
public class McpConnectionController : ControllerBase
{
    private readonly IMcpConnectionEventManager _connectionManager;
    private readonly ILogger<McpConnectionController> _logger;
    private readonly MCPSettings _mcpSettings;

    public McpConnectionController(
        IMcpConnectionEventManager connectionManager,
        ILogger<McpConnectionController> logger,
        IOptions<MCPSettings> mcpSettings)
    {
        _connectionManager = connectionManager;
        _logger = logger;
        _mcpSettings = mcpSettings.Value;
    }

    private IActionResult? CheckMcpEnabled()
    {
        if (!_mcpSettings.Enabled)
        {
            return StatusCode(503, new ErrorResponse
            {
                Error = "MCP operations are currently disabled. Please enable MCP in settings to use this feature.",
                ExceptionType = "ServiceUnavailable"
            });
        }
        return null;
    }

    /// <summary>
    /// Lists all active MCP connections.
    /// </summary>
    [HttpGet("list")]
    [AuthorizeArmOperation(Constants.ArmOperations.AgentThreadWriteActionId)]
    [ProducesResponseType(typeof(IEnumerable<McpConnectionResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public IActionResult ListConnections()
    {
        var mcpCheck = CheckMcpEnabled();
        if (mcpCheck != null) return mcpCheck;

        try
        {
            var connections = _connectionManager.GetActiveConnections();
            var response = connections.Select(MapToResponse);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to list MCP connections");
            return StatusCode(500, CreateErrorResponse(ex));
        }
    }

    /// <summary>
    /// Gets a specific MCP connection by ID.
    /// </summary>
    [HttpGet("{id}")]
    [AuthorizeArmOperation(Constants.ArmOperations.AgentThreadWriteActionId)]
    [ProducesResponseType(typeof(McpConnectionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> GetConnection(string id)
    {
        var mcpCheck = CheckMcpEnabled();
        if (mcpCheck != null) return mcpCheck;

        try
        {
            var connection = await _connectionManager.GetConnectionAsync(id);

            if (connection == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = $"Connection '{id}' not found",
                    ExceptionType = "NotFound"
                });
            }

            var response = MapToResponse(connection);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to get MCP connection: {Id}", id);
            return StatusCode(500, CreateErrorResponse(ex));
        }
    }

    private static ErrorResponse CreateErrorResponse(Exception ex)
    {
        return new ErrorResponse
        {
            Error = ex.Message,
            ExceptionType = ex.GetType().FullName ?? ex.GetType().Name,
            StackTrace = ex.StackTrace,
            InnerException = ex.InnerException != null
                ? new ErrorResponse
                {
                    Error = ex.InnerException.Message,
                    ExceptionType = ex.InnerException.GetType().FullName ?? ex.InnerException.GetType().Name,
                    StackTrace = ex.InnerException.StackTrace
                }
                : null
        };
    }

    private McpConnectionResponse MapToResponse(McpConnection connection)
    {
        // Use metadata type if available
        // Otherwise fall back to transport-based detection
        var transportType = connection.Metadata?.Type.ToString().ToLowerInvariant() ?? connection.ClientTransport switch
        {
            HttpClientTransport => "http",
            StdioClientTransport => "stdio",
            _ => "unknown"
        };

        string? endpoint = null;
        if (connection.ClientTransport is HttpClientTransport httpTransport)
        {
            // Use metadata endpoint if available, otherwise try to extract from transport
            endpoint = connection.Metadata?.Endpoint;
        }

        return new McpConnectionResponse
        {
            ConnectionId = connection.Id,
            Name = connection.Id,
            Type = transportType,
            Endpoint = endpoint,
            Status = connection.Status.ToString(),
            ErrorMessage = connection.ErrorMessage,
            ToolCount = connection.Tools?.Count ?? 0,
            Tools = connection.Tools?.Select(t => new ToolInfo
            {
                // IMPORTANT: Use the prefixed name that ToolFactory registers with
                // Format: {connectionId}_{toolName}
                // This matches the format used by ToolsRepository.GetMcpToolSignature()
                Name = $"{connection.Id}_{t.Name}",
                Description = t.Description ?? string.Empty,
                Parameters = new Dictionary<string, object?>() // AITool schema is complex, simplified for now
            }).ToList(),
            ServerInstructions = connection.ServerInstructions,
            LastHeartbeat = connection.LastHeartbeat,
            AuthenticationType = connection.Authentication?.Type.ToString() ?? "None",
            Description = connection.Metadata?.Description,
            ServiceType = connection.Metadata?.ServiceType
        };
    }
}

/// <summary>
/// Response model for MCP connection.
/// </summary>
public class McpConnectionResponse
{
    public string ConnectionId { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string? Endpoint { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }
    public int ToolCount { get; set; }
    public List<ToolInfo>? Tools { get; set; }
    public string? ServerInstructions { get; set; }
    public DateTimeOffset LastHeartbeat { get; set; }
    public string AuthenticationType { get; set; } = "None";

    /// <summary>
    /// Description of the connection purpose or functionality.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Service type for categorization purposes.
    /// This is a free-form string and is not validated.
    /// </summary>
    public string? ServiceType { get; set; }
}

/// <summary>
/// Information about a tool.
/// </summary>
public class ToolInfo
{
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public object? Parameters { get; set; }
}

/// <summary>
/// Response model for testing an MCP connection.
/// </summary>
public class McpConnectionTestResponse
{
    public string ConnectionId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long ResponseTimeMs { get; set; }
    public string? Error { get; set; }
}

/// <summary>
/// Error response with full exception details.
/// </summary>
public class ErrorResponse
{
    public string Error { get; set; } = string.Empty;
    public string? ExceptionType { get; set; }
    public string? StackTrace { get; set; }
    public ErrorResponse? InnerException { get; set; }
}
