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
        // Check connection health before executing the tool
        // If disconnected, attempt to reconnect
        if (_healthService != null)
        {
            var connection = _healthService.FindConnectionByToolSignature(_newName);
            if (connection != null)
            {
                await _healthService.ValidateConnectionHealthAsync(connection, _newName);
            }
        }

        // Delegate to the original tool for actual execution
        // The original tool knows its actual MCP name (without prefix)
        var rawResult = await _originalTool.InvokeAsync(arguments, cancellationToken);

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
                throw new InvalidOperationException(
                    $"MCP tool '{_newName}' returned multiple content items, which is not supported.");
            }
            else
            {
                var firstContent = callToolResult.Content.First();
                if (firstContent is TextContentBlock textContentBlock)
                {
                    return textContentBlock.Text;
                }
                else
                {
                    return firstContent.ToAIContent()?.ToString();
                }
            }
        }
        return rawResult;
    }
}
