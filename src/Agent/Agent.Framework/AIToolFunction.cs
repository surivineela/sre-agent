// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

/// <summary>
/// Creates a tool from AIFunction
/// </summary>
public abstract class AIToolFunction<T, TInitializationParameter, TContext> : AIFunction, IDeferredToolFunction<TContext>
    where T : AIToolFunction<T, TInitializationParameter, TContext>, new()
    where TInitializationParameter : class?
    where TContext : class
{
    private static T Instance { get; } = new T();

    public static T GetInstance(TInitializationParameter? initializationParameter)
    {
        if (initializationParameter != null)
        {
            Instance.Initialize(initializationParameter);
        }

        return Instance;
    }

    public virtual void Initialize(TInitializationParameter? initializationParameter)
    {
    }

    MethodInfo? IDeferredToolFunction<TContext>.MethodInfo => default;

    string IDeferredToolFunction<TContext>.GetPluginCategory()
    {
        return string.Empty;
    }

    string IDeferredToolFunction<TContext>.GetPluginName()
    {
        return Instance.Name;
    }

    string IDeferredToolFunction<TContext>.GetPluginResourceType()
    {
        return string.Empty;
    }

    AIFunction IDeferredToolFunction<TContext>.GetToolFunction(Guid? threadId, Agent<TContext>? agent)
    {
        return Instance;
    }

    AIFunction IDeferredToolFunction<TContext>.GetToolFunction(Guid? threadId, string? agentMode, Agent<TContext>? agent)
    {
        return Instance;
    }
}

public abstract class AIToolFunction<T, TContext> : AIToolFunction<T, object?, TContext>
    where T : AIToolFunction<T, TContext>, new()
    where TContext : class
{
    public static T GetInstance()
    {
        return GetInstance(null);
    }
}
