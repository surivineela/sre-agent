using System.Text.Json;

using Agent.Runtime.Interfaces;

using Microsoft.Extensions.AI;
using ModelContextProtocol;
using ModelContextProtocol.Protocol;

namespace Agent.Runtime.Services.Mcp;

/// <summary>
/// Wrapper for MCP tools that renames them with a connection-specific prefix
/// while delegating execution to the original tool.
/// </summary>
public class McpToolAIFunction : AIFunction
{
    private readonly AIFunction _originalTool;
    private readonly string _newName;
    private readonly IMcpConnectionHealthService? _healthService;

    public McpToolAIFunction(string newName, AIFunction originalTool, IMcpConnectionHealthService? healthService = null)
    {
        _newName = newName;
        _originalTool = originalTool;
        _healthService = healthService;
    }

    public override string Name => _newName;
    public override string Description => _originalTool.Description;
    public override IReadOnlyDictionary<string, object?> AdditionalProperties => _originalTool.AdditionalProperties;
    public override JsonElement JsonSchema => _originalTool.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => _originalTool.JsonSerializerOptions;
    public override JsonElement? ReturnJsonSchema => _originalTool.ReturnJsonSchema;

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var toolToInvoke = _originalTool;

        // Check connection health before executing the tool
        // If disconnected, attempt to reconnect
        if (_healthService != null)
        {
            var connection = _healthService.FindConnectionByToolSignature(_newName);
            if (connection != null)
            {
                var validatedConnection = await _healthService.ValidateConnectionHealthAsync(connection, _newName);

                // If reconnection happened, the original tool reference is stale.
                // We need to get the refreshed tool from the new connection.
                if (validatedConnection != connection && validatedConnection.Tools != null)
                {
                    // Extract the original tool name (without the connection prefix)
                    // Tool signature format: {connectionId}_{originalToolName}
                    var originalToolName = _originalTool.Name;
                    var refreshedTool = validatedConnection.Tools
                        .OfType<AIFunction>()
                        .FirstOrDefault(t => t.Name == originalToolName);

                    if (refreshedTool != null)
                    {
                        toolToInvoke = refreshedTool;
                    }
                }
            }
        }

        // The tool knows its actual MCP name (without prefix)
        var rawResult = await toolToInvoke.InvokeAsync(arguments, cancellationToken);

        // According to MCP SDK (0.4.1), the result could be either:
        // - a AIContent (TextContent, ImageContent, etc)
        // - list of AIContent
        // - a JsonElement representing a raw CallToolResult
        // Here we will extract the text if it has only text contents. Otherwise, return as is.
        if (rawResult is TextContent textContent)
        {
            return textContent.Text;
        }
        else if (rawResult is IEnumerable<AIContent> aiContents)
        {
            return string.Join("\n------\n", aiContents.Select(c => c is TextContent t ? t.Text : $"<Unsupported {c.GetType().Name}>"));
        }
        else
        {
            return rawResult;
        }
    }
}
