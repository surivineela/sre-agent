// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Framework;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Reasoning;

// Most tools are injected as transient, so we can defer the creation of the tool function until it's actually needed.
public sealed class DeferredToolFunction
{
    private readonly IServiceProvider _sp;
    private readonly MethodInfo _methodInfo;
    private readonly Type _pluginType;
    private readonly string _name;

    public DeferredToolFunction(IServiceProvider sp, Type pluginType, MethodInfo methodInfo, string name)
    {
        _sp = sp;
        _pluginType = pluginType;
        _methodInfo = methodInfo;
        _name = name;
    }

    public AIFunction GetToolFunction(Guid? threadId = null)
    {
        var instance = _sp.GetRequiredService(_pluginType);

        if (threadId is not null)
        {
            // If the plugin type has a ThreadId field, set it to the provided threadId.
            var threadIdProperty = _pluginType.GetProperty("ThreadId", BindingFlags.Instance | BindingFlags.Public);
            if (threadIdProperty is not null && threadIdProperty.PropertyType == typeof(Guid?))
            {
                threadIdProperty.SetValue(instance, threadId);
            }
        }

        return AIFunctionFactory.Create(_methodInfo, instance, name: _name);
    }
}

/// <summary>
/// A default implementation of IToolFactory that automatically scans for tools in the provided assemblies.
/// Only classes with the AgentToolPluginAttribute are considered as tools.
/// </summary>
public class ToolFactory : IToolFactory
{
    private readonly ILogger<ToolFactory> _logger;
    private readonly Dictionary<string, DeferredToolFunction> _tools = [];
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<Assembly> _assemblies;

    public ToolFactory(
        ILogger<ToolFactory> logger,
        IServiceProvider serviceProvider,
        IEnumerable<Assembly> assembliesToScan
    )
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _assemblies = assembliesToScan;
        FindAndRegisterAllTools(BehaviorOnNameConflict.ThrowException);
    }

    private void FindAndRegisterAllTools(BehaviorOnNameConflict onNameConflict)
    {
        var plugins = _assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsDefined(typeof(AgentToolPluginAttribute)));

        if (!plugins.Any())
        {
            throw new Exception("No tool plugins found. Ensure that your assemblies are loaded and contain classes with AgentToolPluginAttribute.");
        }

        foreach (var pluginType in plugins)
        {
            try
            {
                var attribute = pluginType.GetCustomAttribute<AgentToolPluginAttribute>();
                if (attribute is null)
                {
                    _logger.LogInternalWarning("Type {pluginType} does not have AgentToolPluginAttribute.", pluginType.FullName);
                    continue;
                }

                var flags = BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly;

                // Get all public methods with Description attribute
                var methodsToRegister = pluginType.GetMethods(flags)
                    .Where(m => m.GetCustomAttribute<System.ComponentModel.DescriptionAttribute>() != null)
                    .ToList();

                foreach (var method in methodsToRegister)
                {
                    var functionName = method.Name.EndsWith("Async")
                        ? method.Name[..^5]
                        : method.Name;
                    var tool = new DeferredToolFunction(_serviceProvider, pluginType, method, functionName);
                    if (!RegisterAIFunction(functionName, tool, onNameConflict))
                    {
                        _logger.LogInternalWarning("Failed to register tool {functionName} from type {pluginType} due to name conflict.", functionName, pluginType.FullName);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to register tool from type {pluginType}.", pluginType.FullName);
            }
        }
    }

    public AIFunction FindAIFunction(string name)
    {
        return DoFindAIFunction(name, null);
    }

    private bool RegisterAIFunction(string name, DeferredToolFunction function, BehaviorOnNameConflict onNameConflict)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogInternalError("Function name cannot be null or whitespace.");
            return false;
        }

        if (_tools.ContainsKey(name))
        {
            switch (onNameConflict)
            {
                case BehaviorOnNameConflict.ThrowException:
                    throw new InvalidOperationException($"Function '{name}' already exists.");
                case BehaviorOnNameConflict.Ignore:
                    _logger.LogInternalWarning("Function '{functionName}' already exists. Ignoring the new function.", name);
                    return false;
                case BehaviorOnNameConflict.Overwrite:
                    _logger.LogInternalWarning("Function '{functionName}' already exists. Overwriting the existing function.", name);
                    break;
            }
        }

        _tools[name] = function;
        _logger.LogInternalInformation("Function '{functionName}' registered successfully.", name);
        return true;
    }

    public bool TryFindAIFunction(string name, out AIFunction? function)
    {
        if (_tools.TryGetValue(name, out var deferredToolFunction))
        {
            function = deferredToolFunction.GetToolFunction();
            return true;
        }

        _logger.LogInternalError("Function '{functionName}' not found.", name);
        function = null;
        return false;
    }

    public bool HasAIFunction(string name)
    {
        return _tools.ContainsKey(name);
    }

    public AIFunction FindAIFunction(string name, Guid threadId)
    {
        return DoFindAIFunction(name, threadId);
    }

    private AIFunction DoFindAIFunction(string name, Guid? threadId = null)
    {
        if (_tools.TryGetValue(name, out var function))
        {
            return function.GetToolFunction(threadId);
        }

        _logger.LogInternalError("Function '{functionName}' not found.", name);
        throw new KeyNotFoundException($"Function '{name}' not found.");
    }
}
