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

    /// <summary>
    /// Creates and adds a new MCP connection.
    /// </summary>
    [HttpPost]
    [AuthorizeArmOperation(Constants.ArmOperations.AgentThreadWriteActionId)]
    [ProducesResponseType(typeof(McpConnectionResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> CreateConnection([FromBody] McpConnectionRequest request)
    {
        var mcpCheck = CheckMcpEnabled();
        if (mcpCheck != null) return mcpCheck;

        try
        {
            _logger.LogInternalInformation("Creating MCP connection: {Name}", request.Name);

            // Parse the type string to enum with case-insensitive matching
            if (!Enum.TryParse<McpTransportType>(request.Type, ignoreCase: true, out var transportType))
            {
                return BadRequest(new ErrorResponse
                {
                    Error = $"Invalid transport type '{request.Type}'. Valid values are: {string.Join(", ", Enum.GetNames<McpTransportType>())}",
                    ExceptionType = "ArgumentException"
                });
            }

            var connection = await _connectionManager.CreateAndAddConnectionAsync(
                request.Name,
                transportType,
                request.Endpoint,
                request.Command,
                request.Arguments,
                request.WorkingDirectory,
                request.Authentication,
                request.Headers,
                request.Description,
                request.ServiceType);

            var response = MapToResponse(connection);

            _logger.LogInternalInformation("Successfully created MCP connection: {Id}", connection.Id);
            return CreatedAtAction(nameof(GetConnection), new { id = connection.Id }, response);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to create MCP connection: {Name}", request.Name);
            return StatusCode(500, CreateErrorResponse(ex));
        }
    }

    /// <summary>
    /// Disconnects and removes an MCP connection by ID.
    /// </summary>
    [HttpDelete("{id}")]
    [AuthorizeArmOperation(Constants.ArmOperations.AgentThreadWriteActionId)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> DeleteConnection(string id)
    {
        var mcpCheck = CheckMcpEnabled();
        if (mcpCheck != null) return mcpCheck;

        try
        {
            _logger.LogInternalInformation("Removing MCP connection: {Id}", id);

            var removed = await _connectionManager.RemoveConnectionAsync(id);

            if (!removed)
            {
                return NotFound(new ErrorResponse
                {
                    Error = $"Connection '{id}' not found",
                    ExceptionType = "NotFound"
                });
            }

            _logger.LogInternalInformation("Successfully removed MCP connection: {Id}", id);
            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to remove MCP connection: {Id}", id);
            return StatusCode(500, CreateErrorResponse(ex));
        }
    }

    /// <summary>
    /// Refreshes an MCP connection by reconnecting and reloading tools.
    /// </summary>
    [HttpPost("{id}/refresh")]
    [AuthorizeArmOperation(Constants.ArmOperations.AgentThreadWriteActionId)]
    [ProducesResponseType(typeof(McpConnectionRefreshResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> RefreshConnection(string id)
    {
        var mcpCheck = CheckMcpEnabled();
        if (mcpCheck != null) return mcpCheck;

        try
        {
            _logger.LogInternalInformation("Refreshing MCP connection: {Id}", id);

            var startTime = DateTimeOffset.UtcNow;
            var oldConnection = await _connectionManager.GetConnectionAsync(id);

            if (oldConnection == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = $"Connection '{id}' not found",
                    ExceptionType = "NotFound"
                });
            }

            var oldToolCount = oldConnection.Tools?.Count ?? 0;

            var refreshedConnection = await _connectionManager.RefreshConnectionAsync(id);
            var elapsed = DateTimeOffset.UtcNow - startTime;

            var response = new McpConnectionRefreshResponse
            {
                ConnectionId = refreshedConnection.Id,
                Success = true,
                Message = "Connection refreshed successfully",
                RefreshTimeMs = (long)elapsed.TotalMilliseconds,
                OldToolCount = oldToolCount,
                NewToolCount = refreshedConnection.Tools?.Count ?? 0,
                Connection = MapToResponse(refreshedConnection)
            };

            _logger.LogInternalInformation(
                "Successfully refreshed MCP connection '{Id}' - Old tools: {OldCount}, New tools: {NewCount}, Time: {Time}ms",
                id,
                oldToolCount,
                response.NewToolCount,
                response.RefreshTimeMs);

            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to refresh MCP connection: {Id}", id);
            return StatusCode(500, CreateErrorResponse(ex));
        }
    }

    /// <summary>
    /// Tests an MCP connection by attempting to ping the server.
    /// </summary>
    [HttpPost("test/{id}")]
    [AuthorizeArmOperation(Constants.ArmOperations.AgentThreadWriteActionId)]
    [ProducesResponseType(typeof(McpConnectionTestResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status500InternalServerError)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> TestConnection(string id)
    {
        var mcpCheck = CheckMcpEnabled();
        if (mcpCheck != null) return mcpCheck;

        try
        {
            _logger.LogInternalInformation("Testing MCP connection: {Id}", id);

            var connection = await _connectionManager.GetConnectionAsync(id);

            if (connection == null)
            {
                return NotFound(new ErrorResponse
                {
                    Error = $"Connection '{id}' not found",
                    ExceptionType = "NotFound"
                });
            }

            var testResult = new McpConnectionTestResponse
            {
                ConnectionId = connection.Id,
                Success = false,
                Message = "Unknown",
                ResponseTimeMs = 0
            };

            var startTime = DateTimeOffset.UtcNow;

            try
            {
                if (connection.Client == null)
                {
                    testResult.Success = false;
                    testResult.Message = "Connection client is not initialized";
                    return Ok(testResult);
                }

                // Attempt to ping the server
                await connection.Client.PingAsync();

                var elapsed = DateTimeOffset.UtcNow - startTime;
                testResult.Success = true;
                testResult.Message = "Ping successful";
                testResult.ResponseTimeMs = (long)elapsed.TotalMilliseconds;

                _logger.LogInternalInformation(
                    "Successfully tested MCP connection '{Id}' - Response time: {ResponseTime}ms",
                    id,
                    testResult.ResponseTimeMs);
            }
            catch (Exception pingEx)
            {
                var elapsed = DateTimeOffset.UtcNow - startTime;
                testResult.Success = false;
                testResult.Message = $"Ping failed: {pingEx.Message}";
                testResult.ResponseTimeMs = (long)elapsed.TotalMilliseconds;
                testResult.Error = pingEx.Message;

                _logger.LogInternalWarning(
                    pingEx,
                    "Failed to ping MCP connection '{Id}'",
                    id);
            }

            return Ok(testResult);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to test MCP connection: {Id}", id);
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
        // Use metadata type if available (preserves "sse" vs "http" distinction)
        // Otherwise fall back to transport-based detection
        var transportType = connection.Metadata?.Type ?? connection.ClientTransport switch
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
/// Request model for creating an MCP connection.
/// </summary>
public class McpConnectionRequest
{
    /// <summary>
    /// Name of the connection.
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Type of connection: "sse" or "stdio".
    /// </summary>
    public required string Type { get; set; }

    /// <summary>
    /// Endpoint URL (required for SSE connections).
    /// </summary>
    public string? Endpoint { get; set; }

    /// <summary>
    /// Command to execute (required for STDIO connections).
    /// </summary>
    public string? Command { get; set; }

    /// <summary>
    /// Arguments for the command (for STDIO connections).
    /// </summary>
    public string[]? Arguments { get; set; }

    /// <summary>
    /// Working directory for the command (for STDIO connections).
    /// </summary>
    public string? WorkingDirectory { get; set; }

    /// <summary>
    /// Authentication configuration for the connection.
    /// Use either this property OR the Headers property, not both.
    /// </summary>
    public McpAuthenticationConfig? Authentication { get; set; }

    /// <summary>
    /// Direct headers to be sent with every request (LogicApps-style).
    /// Use this for simple header-based authentication instead of the Authentication object.
    /// Example: { "X-API-Key": "your-key" }
    /// Use either this property OR the Authentication property, not both.
    /// </summary>
    public Dictionary<string, string>? Headers { get; set; }

    /// <summary>
    /// Description of the connection purpose or functionality.
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Service type for categorization purposes (e.g., "GitHub", "Datadog", "Custom").
    /// This is a free-form string and is not validated.
    /// </summary>
    public string? ServiceType { get; set; }
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

    /// <summary>
    /// Maximum number of tools that can be selected from this connection.
    /// This is the global setting from MCPSettings.MaxToolsPerConnection.
    /// </summary>
    public int MaxToolsPerConnection { get; set; }
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
/// Response model for refreshing an MCP connection.
/// </summary>
public class McpConnectionRefreshResponse
{
    public string ConnectionId { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public long RefreshTimeMs { get; set; }
    public int OldToolCount { get; set; }
    public int NewToolCount { get; set; }
    public McpConnectionResponse? Connection { get; set; }
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
