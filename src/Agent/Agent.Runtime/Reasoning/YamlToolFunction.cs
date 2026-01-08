// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Framework;
using Agent.Plugins.Tools;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Deferred tool function wrapper for YAML tools.
/// Implements IDeferredToolFunction to support lazy tool creation per thread/agent.
/// </summary>
public class YamlToolFunction<TContext> : IDeferredToolFunction<TContext> where TContext : class
{
    private readonly YamlToolDefinitionBase _toolDef;
    private readonly IYamlToolExecutor _executor;

    // MethodInfo is not needed - factory pattern eliminates reflection
    public MethodInfo? MethodInfo => null;

    /// <summary>
    /// Creates a new YamlToolFunction with the given tool definition and executor.
    /// Use <see cref="IYamlToolFunctionFactory{TContext}"/> to create instances.
    /// </summary>
    /// <param name="toolDef">The YAML tool definition.</param>
    /// <param name="executor">The executor created by the appropriate factory.</param>
    public YamlToolFunction(YamlToolDefinitionBase toolDef, IYamlToolExecutor executor)
    {
        _toolDef = toolDef ?? throw new ArgumentNullException(nameof(toolDef));
        _executor = executor ?? throw new ArgumentNullException(nameof(executor));
    }

    public AIFunction GetToolFunction(Guid? threadId = null, Agent<TContext>? agent = null)
    {
        return GetToolFunction(threadId, null, agent);
    }

    public AIFunction GetToolFunction(Guid? threadId, string? agentMode, Agent<TContext>? agent = null)
    {
        return new YamlToolAIFunction(_toolDef, _executor, threadId);
    }

    public string GetPluginCategory()
    {
        return _toolDef.Type;
    }

    public string GetPluginResourceType()
    {
        return _toolDef.Type;
    }

    public string GetAggregateToolType()
    {
        return _toolDef.Type;
    }

    public string GetPluginName()
    {
        return _toolDef.Name;
    }
}
