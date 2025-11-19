// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Wrapper class that enhances an AIFunction with a modified description.
/// Similar to YamlAwareAIFunction but for any AIFunction that needs description enhancement.
/// </summary>
internal class DescriptionEnhancedAIFunction : AIFunction
{
    private readonly AIFunction _innerFunction;
    private readonly string _enhancedDescription;

    public DescriptionEnhancedAIFunction(AIFunction innerFunction, string enhancedDescription)
    {
        _innerFunction = innerFunction;
        _enhancedDescription = enhancedDescription;
    }

    /// <summary>
    /// Gets the original function that was wrapped.
    /// </summary>
    public AIFunction OriginalFunction => _innerFunction;

    public override string Name => _innerFunction.Name;

    public override string Description => _enhancedDescription;

    public override System.Text.Json.JsonElement JsonSchema => _innerFunction.JsonSchema;

    public override IReadOnlyDictionary<string, object?> AdditionalProperties => _innerFunction.AdditionalProperties;

    public override System.Reflection.MethodInfo? UnderlyingMethod => _innerFunction.UnderlyingMethod;

    protected override ValueTask<object?> InvokeCoreAsync(AIFunctionArguments arguments, CancellationToken cancellationToken)
    {
        return _innerFunction.InvokeAsync(arguments, cancellationToken);
    }
}
