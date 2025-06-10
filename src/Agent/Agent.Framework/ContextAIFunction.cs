// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class ContextAIFunction<TContext> : AIFunction where TContext : class
{
    private readonly AIFunction _innerFunction;
    private readonly ContextToolTarget<TContext> _target;

    private ContextAIFunction(
        MethodInfo method,
        ContextToolTarget<TContext> target,
        string? name = null,
        string? description = null)
    {
        _target = target;
        _innerFunction = AIFunctionFactory.Create(method, target, name: name, description: description);
    }

    public static ContextAIFunction<TContext> Create(Delegate method, string? name = null, string? description = null)
    {
        var target = CheckTargetTypeContextAware(method.Method, method.Target);
        return new ContextAIFunction<TContext>(method.Method, target, name, description);
    }

    public static ContextAIFunction<TContext> Create(MethodInfo method, object? methodTarget, string? name = null, string? description = null)
    {
        var target = CheckTargetTypeContextAware(method, methodTarget);
        return new ContextAIFunction<TContext>(method, target, name, description);
    }

    private static ContextToolTarget<TContext> CheckTargetTypeContextAware(MethodInfo method, object? target)
    {
        if (target is null)
        {
            throw new InvalidOperationException("target is null, ContextAIFunction is not supported for static methods");
        }

        var functionTargetType = method.DeclaringType;
        var toolPluginGenericType = typeof(ContextToolTarget<>);

        if (functionTargetType != null
            && functionTargetType.BaseType != null
            && functionTargetType.BaseType.IsGenericType
            && functionTargetType.BaseType.GetGenericTypeDefinition() == toolPluginGenericType
            && functionTargetType.BaseType.GetGenericArguments().Single() == typeof(TContext)  // already checked generic type is ToolPlugin, which only has one generic argument
            && functionTargetType == target.GetType())
        {
            if (target is ContextToolTarget<TContext> toolPlugin)
            {
                return toolPlugin;
            }

            throw new InvalidOperationException($"target not assignable to {typeof(ContextToolTarget<TContext>)}");
        }

        throw new InvalidOperationException("Target type is not context aware");
    }

    public void SetContext(TContext? context)
    {
        _target.Context = context;
    }

    #region AITool properties
    public override string Name => _innerFunction.Name;
    public override string Description => _innerFunction.Description;
    public override IReadOnlyDictionary<string, object?> AdditionalProperties => _innerFunction.AdditionalProperties;
    #endregion

    #region AIFunction override
    public override JsonElement JsonSchema => _innerFunction.JsonSchema;
    public override JsonSerializerOptions JsonSerializerOptions => _innerFunction.JsonSerializerOptions;
    public override MethodInfo? UnderlyingMethod => _innerFunction.UnderlyingMethod;

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        return _innerFunction.InvokeAsync(arguments, cancellationToken);
    }
    #endregion
}
