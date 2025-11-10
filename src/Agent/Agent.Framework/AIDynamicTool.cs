// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.AI;

namespace Agent.Framework;

public class AIDynamicTool<TContext> : IDeferredToolFunction<TContext> where TContext : class
{
    private readonly Func<Guid?, string?, Agent<TContext>?, AIFunction> _functionFactory;

    public AIDynamicTool(Func<Guid?, string?, Agent<TContext>?, AIFunction> functionFactory)
    {
        _functionFactory = functionFactory;
    }

    MethodInfo? IDeferredToolFunction<TContext>.MethodInfo => default;

    string IDeferredToolFunction<TContext>.GetPluginCategory()
    {
        return string.Empty;
    }

    string IDeferredToolFunction<TContext>.GetPluginName()
    {
        return string.Empty;
    }

    string IDeferredToolFunction<TContext>.GetPluginResourceType()
    {
        return string.Empty;
    }

    AIFunction IDeferredToolFunction<TContext>.GetToolFunction(Guid? threadId, Agent<TContext>? agent)
    {
        return _functionFactory(threadId, string.Empty, agent!);
    }

    AIFunction IDeferredToolFunction<TContext>.GetToolFunction(Guid? threadId, string? agentMode, Agent<TContext>? agent)
    {
        return _functionFactory(threadId, agentMode, agent);
    }
}
