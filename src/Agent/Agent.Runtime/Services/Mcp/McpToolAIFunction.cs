using Agent.Runtime.Interfaces;
using Microsoft.Extensions.AI;

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

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        // Check connection health before executing the tool
        if (_healthService != null)
        {
            var connection = _healthService.FindConnectionByToolSignature(_newName);
            if (connection != null)
            {
                _healthService.ValidateConnectionHealth(connection, _newName);
            }
        }

        // Delegate to the original tool for actual execution
        // The original tool knows its actual MCP name (without prefix)
        return _originalTool.InvokeAsync(arguments, cancellationToken);
    }
}
