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

        // The result type of the MCP client is handled poorly by the SDK (0.4.0), which is just a JsonElement
        // So we need to deserialize it into CallToolResult and extract the Content
        if (rawResult is JsonElement jsonElement)
        {
            var callToolResult = jsonElement.Deserialize<CallToolResult>();
            if (callToolResult is null)
            {
                return rawResult;
            }
            else if (callToolResult.Content.Count > 1)
            {
                // Combine multiple content items into a single response
                // TODO: Handle if the content is an image or not.
                rawResult = string.Join("\n", callToolResult.Content.Select(c => c.ToAIContent()).OfType<TextContent>().Select(tc => tc.Text));
            }
            else
            {
                rawResult = callToolResult.Content.First().ToAIContent();
            }
        }
        if (rawResult is TextContent textContent)
        {
            return textContent.Text;
        }
        return rawResult;
    }
}
