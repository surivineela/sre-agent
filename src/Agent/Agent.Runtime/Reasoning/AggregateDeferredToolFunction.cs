// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// A specialized deferred tool function for aggregate tools that supports dynamic description updates.
/// This allows the description to be updated as child tools are registered without recreating the entire function.
/// Uses composition over inheritance by wrapping a DeferredToolFunction.
/// </summary>
public sealed class AggregateDeferredToolFunction<TContext> : IDeferredToolFunction<TContext>
    where TContext : class
{
    private readonly DeferredToolFunction<TContext> _innerFunction;
    private string? _aggregatedDescription;

    public AggregateDeferredToolFunction(
        IServiceProvider sp,
        Type pluginType,
        MethodInfo methodInfo,
        string name)
    {
        _innerFunction = new DeferredToolFunction<TContext>(sp, pluginType, methodInfo, name);
    }

    /// <summary>
    /// Gets the tool function with the current aggregated description.
    /// </summary>
    public AIFunction GetToolFunction(Guid? threadId = null, Agent<TContext>? agent = null)
    {
        return GetToolFunction(threadId, null);
    }

    /// <summary>
    /// Gets the tool function with the current aggregated description and agent mode support.
    /// </summary>
    public AIFunction GetToolFunction(Guid? threadId, string? agentMode, Agent<TContext>? agent = null)
    {
        // Get the function from the inner DeferredToolFunction
        var baseFunction = _innerFunction.GetToolFunction(threadId, agentMode);

        // If we have an aggregated description, wrap with enhanced description
        if (!string.IsNullOrEmpty(_aggregatedDescription))
        {
            return new DescriptionEnhancedAIFunction(baseFunction, _aggregatedDescription);
        }

        // No aggregated description available, return base function
        return baseFunction;
    }

    /// <summary>
    /// Updates the aggregated description. This is called when new tools are registered to the aggregate.
    /// </summary>
    public void UpdateDescription(string description)
    {
        _aggregatedDescription = description;
    }

    // Delegate IDeferredToolFunction interface members to the inner function
    public string GetPluginCategory() => _innerFunction.GetPluginCategory();
    public string GetPluginResourceType() => _innerFunction.GetPluginResourceType();
    public string GetPluginName() => _innerFunction.GetPluginName();
    public MethodInfo? MethodInfo => _innerFunction.MethodInfo;
}
