// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Framework;
using Agent.Plugins.Tools;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// AIFunction implementation for YAML tools.
/// Wraps an IYamlToolExecutor instance created by YamlToolFunction.
/// </summary>
public class YamlToolAIFunction : AIFunction
{
    private readonly YamlToolDefinitionBase _toolDef;
    private readonly IYamlToolExecutor _executor;
    private readonly Lazy<JsonElement> _customSchema;
    private readonly Guid? _threadId;

    public override string Name => _toolDef.Name;
    public override string Description => _toolDef.Description;
    public override JsonElement JsonSchema => _customSchema.Value;
    public override IReadOnlyDictionary<string, object?> AdditionalProperties => new Dictionary<string, object?>();

    public YamlToolAIFunction(
        YamlToolDefinitionBase toolDef,
        IYamlToolExecutor executor,
        Guid? threadId = null)
    {
        _toolDef = toolDef;
        _executor = executor;
        _threadId = threadId;

        // Create schema lazily from executor
        _customSchema = new Lazy<JsonElement>(() => _executor.CreateSchema());
    }

    protected override async ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        var threadId = _threadId?.ToString() ?? string.Empty;

        // Simple delegation - pass threadId and arguments
        var result = await _executor.ExecuteAsync(threadId, arguments);

        return result;
    }
}
