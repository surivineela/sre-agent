// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

/// <summary>
/// Creates a tool from AIFunction
/// </summary>
public class AIToolAdapter : AIFunction, IDeferredToolFunction
{
    public readonly AIFunction _function;

    public AIToolAdapter(AIFunction function)
    {
        _function = function;
    }

    MethodInfo? IDeferredToolFunction.MethodInfo => default;

    string IDeferredToolFunction.GetPluginCategory()
    {
        return string.Empty;
    }

    string IDeferredToolFunction.GetPluginName()
    {
        return _function.Name;
    }

    string IDeferredToolFunction.GetPluginResourceType()
    {
        return string.Empty;
    }

    AIFunction IDeferredToolFunction.GetToolFunction(Guid? threadId)
    {
        return _function;
    }

    AIFunction IDeferredToolFunction.GetToolFunction(Guid? threadId, string? agentMode)
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
