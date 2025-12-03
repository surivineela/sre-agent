using System.Text.Json.Serialization;

namespace Agent.Core.Models.Session;

/// <summary>
/// Request model for MCP proxy WebSocket connection initialization.
/// This message should be sent as the first WebSocket message after connection.
/// </summary>
public class McpConnectionRequest
{
    /// <summary>
    /// The command to execute (e.g., npx, node, uvx).
    /// </summary>
    [JsonPropertyName("cmd")]
    public string Command { get; set; } = string.Empty;

    /// <summary>
    /// Array of command arguments.
    /// </summary>
    [JsonPropertyName("args")]
    public string[] Arguments { get; set; } = Array.Empty<string>();

    /// <summary>
    /// Optional dictionary of environment variables to set for the MCP server process.
    /// </summary>
    [JsonPropertyName("envVars")]
    public Dictionary<string, string>? EnvironmentVariables { get; set; }

    /// <summary>
    /// Optional dictionary of action tokens (scope -> token mapping).
    /// These tokens are made available to the MCP server via the MSI endpoint.
    /// </summary>
    [JsonPropertyName("actionTokens")]
    public Dictionary<string, string>? ActionTokens { get; set; }
}
