// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Framework;
using Agent.Plugins.Tools;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Factory for creating YamlToolFunction instances.
/// Centralizes the logic for resolving executor factories and creating tool functions.
/// </summary>
/// <typeparam name="TContext">The agent context type.</typeparam>
public class YamlToolFunctionFactory<TContext> : IYamlToolFunctionFactory<TContext> where TContext : class
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<IYamlToolExecutorFactory> _factories;

    public YamlToolFunctionFactory(IServiceProvider serviceProvider, IEnumerable<IYamlToolExecutorFactory> factories)
    {
        _serviceProvider = serviceProvider;
        _factories = factories;
    }

    /// <inheritdoc/>
    public YamlToolFunction<TContext> Create(YamlToolDefinitionBase toolDef)
    {
        var factory = _factories.FirstOrDefault(f =>
        {
            var attr = f.GetType().GetCustomAttribute<ToolTypeAttribute>();
            return attr?.Name.Equals(toolDef.Type, StringComparison.OrdinalIgnoreCase) == true;
        }) ?? throw new InvalidOperationException(
                $"No factory registered for tool type '{toolDef.Type}'. " +
                $"Ensure IYamlToolExecutorFactory with [ToolType(\"{toolDef.Type}\")] is registered in DI.");

        var executor = factory.Create(toolDef, _serviceProvider);
        return new YamlToolFunction<TContext>(toolDef, executor);
    }
}
