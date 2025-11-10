// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

/// <summary>
/// Creates a tool from AIFunction
/// </summary>
public class AIToolAdapter<TContext> : AIFunction, IDeferredToolFunction<TContext> where TContext : class
{
    public readonly AIFunction _function;

    public AIToolAdapter(AIFunction function)
    {
        _function = function;
    }

    MethodInfo? IDeferredToolFunction<TContext>.MethodInfo => default;

    string IDeferredToolFunction<TContext>.GetPluginCategory()
    {
        return string.Empty;
    }

    string IDeferredToolFunction<TContext>.GetPluginName()
    {
        return _function.Name;
    }

    string IDeferredToolFunction<TContext>.GetPluginResourceType()
    {
        return string.Empty;
    }

    AIFunction IDeferredToolFunction<TContext>.GetToolFunction(Guid? threadId, Agent<TContext>? agent)
    {
        return _function;
    }

    AIFunction IDeferredToolFunction<TContext>.GetToolFunction(Guid? threadId, string? agentMode, Agent<TContext>? agent)
    {
        return _function;
    }

    protected override ValueTask<object?> InvokeCoreAsync(
        AIFunctionArguments arguments,
        CancellationToken cancellationToken)
    {
        return _function.InvokeAsync(arguments, cancellationToken);
    }
}
