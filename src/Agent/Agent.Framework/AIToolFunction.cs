// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

/// <summary>
/// Creates a tool from AIFunction
/// </summary>
public abstract class AIToolFunction<T> : AIFunction, IDeferredToolFunction
    where T : AIToolFunction<T>, new()
{
    public static T Instance { get; } = new T();

    MethodInfo? IDeferredToolFunction.MethodInfo => default;

    string IDeferredToolFunction.GetPluginCategory()
    {
        return string.Empty;
    }

    string IDeferredToolFunction.GetPluginName()
    {
        return Instance.Name;
    }

    string IDeferredToolFunction.GetPluginResourceType()
    {
        return string.Empty;
    }

    AIFunction IDeferredToolFunction.GetToolFunction(Guid? threadId)
    {
        return Instance;
    }

    AIFunction IDeferredToolFunction.GetToolFunction(Guid? threadId, string? agentMode)
    {
        return Instance;
    }
}
