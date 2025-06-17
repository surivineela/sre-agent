// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Logging;
using Agent.Plugins;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;

namespace Agent.Runtime.Reasoning;

// Most tools are injected as transient, so we can defer the creation of the tool function until it's actually needed.
public sealed class DeferredToolFunction<TContext> where TContext : class
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
            // Check for public ThreadId property first
            var threadIdPropertyPublic = _pluginType.GetProperty("ThreadId", BindingFlags.Instance | BindingFlags.Public);
            if (threadIdPropertyPublic is not null && threadIdPropertyPublic.PropertyType == typeof(Guid?))
            {
                threadIdPropertyPublic.SetValue(instance, threadId);
            }

            // Check all private fields and if they have ThreadId, set it on those objects
            var privateFields = _pluginType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in privateFields)
            {
                // Get the actual object stored in this private field (e.g., the IArmPlugin instance)
                var fieldValue = field.GetValue(instance);
                if (fieldValue != null)
                {
                    var fieldType = fieldValue.GetType();

                    // Check if this field object (e.g., IArmPlugin) has a public ThreadId property
                    var threadIdProperty = fieldType.GetProperty("ThreadId", BindingFlags.Instance | BindingFlags.Public);
                    if (threadIdProperty != null && threadIdProperty.PropertyType == typeof(Guid?) && threadIdProperty.CanWrite)
                    {
                        threadIdProperty.SetValue(fieldValue, threadId);
                    }

                    // Also check for private ThreadId fields on the field object
                    var threadIdField = fieldType.GetField("ThreadId", BindingFlags.Instance | BindingFlags.NonPublic);
                    if (threadIdField != null && threadIdField.FieldType == typeof(Guid?))
                    {
                        threadIdField.SetValue(fieldValue, threadId);
                    }
                }
            }
        }

        if (instance is ContextToolTarget<TContext> contextToolTarget)
        {
            return ContextAIFunction<TContext>.Create(_methodInfo, contextToolTarget, name: _name);
        }

        return AIFunctionFactory.Create(_methodInfo, instance, name: _name);
    }
}

/// <summary>
/// A default implementation of IToolFactory that automatically scans for tools in the provided assemblies.
/// Only classes with the AgentToolPluginAttribute are considered as tools.
/// </summary>
public class ToolFactory<TContext> : IToolFactory<TContext> where TContext : class
{
    private readonly ILogger<ToolFactory<TContext>> _logger;
    private readonly Dictionary<string, DeferredToolFunction<TContext>> _tools = [];
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly IEnumerable<Assembly> _assemblies;

    public ToolFactory(
        ILogger<ToolFactory<TContext>> logger,
        IServiceProvider serviceProvider,
        IEnumerable<Assembly> assembliesToScan
    )
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _assemblies = assembliesToScan;
        _hostEnvironment = _serviceProvider.GetRequiredService<IHostEnvironment>();
        _configuration = _serviceProvider.GetRequiredService<IConfiguration>();
        FindAndRegisterAllTools(BehaviorOnNameConflict.ThrowException);
    }

    public List<ToolInfo> FetchAvailableToolInfo()
    {
        var result = new List<ToolInfo>();
        foreach (var tool in _tools)
        {
            try
            {
                result.Add(new ToolInfo
                {
                    Name = tool.Key,
                    Description = tool.Value.GetToolFunction()?.Description,
                    Parameters = tool.Value.GetToolFunction()?.UnderlyingMethod?.GetParameters()?.Select(x => x.Name)?.ToArray()
                });
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to fetch tool info for {toolName}.", tool.Key);
            }
        }
        return result;
    }

    private bool ShouldRegisterPlugin(string pluginName, AgentToolPluginAttribute agentToolPluginAttribute)
    {
        if (!agentToolPluginAttribute.IsEnabled)
        {
            _logger.LogInternalWarning("Plugin {toolName} is disabled and will not be registered.", pluginName);
            return false;
        }

        if (agentToolPluginAttribute.IsExperimental && _hostEnvironment != null && !_hostEnvironment.IsDevelopment())
        {
            _logger.LogInternalWarning("Plugin {toolName} is experimental and will not be registered in non-development environments.", pluginName);
            return false;
        }

        // TODO: Set this in a constants file
        var firstPartyTenants = new List<string>() { "33e01921-4d64-4f8c-a055-5bdaffd5e33d", "72f988bf-86f1-41af-91ab-2d7cd011db47" };

        var tenantId = _configuration != null ? _configuration.GetValue("AppSettings:Core:Azure:Crawler:TenantId", string.Empty) : string.Empty;
        var isFirstParty = !string.IsNullOrWhiteSpace(tenantId) && firstPartyTenants.Contains(tenantId);

        if (agentToolPluginAttribute.IsFirstPartyOnly && !isFirstParty)
        {
            _logger.LogInternalWarning("Plugin {pluginName} is marked as first-party only and will not be registered in non-first-party environments.", pluginName);
            return false;
        }

        return true;
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

                if (!ShouldRegisterPlugin(pluginName: pluginType.Name, attribute))
                {
                    _logger.LogInternalInformation("Skipping registration of plugin {pluginName} due to attribute conditions.", pluginType.Name);
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
                    var tool = new DeferredToolFunction<TContext>(_serviceProvider, pluginType, method, functionName);
                    if (!RegisterTool(functionName, tool, onNameConflict))
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

    public AIFunction GetTool(string name)
    {
        return DoFindAIFunction(name, null);
    }

    private bool RegisterTool(string name, DeferredToolFunction<TContext> function, BehaviorOnNameConflict onNameConflict)
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

    public bool TryFindTool(string name, out AIFunction? function)
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

    public bool HasTool(string name)
    {
        return _tools.ContainsKey(name);
    }

    public AIFunction GetTool(string name, Guid threadId)
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
