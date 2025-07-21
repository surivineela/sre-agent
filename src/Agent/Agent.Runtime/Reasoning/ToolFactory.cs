// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Framework;
using Agent.Plugins;
using Agent.Plugins.Tools;
using Agent.Runtime.Reasoning.Models; // Using the new location for YAML models
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// A unified class that creates a tool from either reflection (a MethodInfo)
/// or a configuration object (a YamlToolDefinition).
/// </summary>
public sealed class DeferredToolFunction<TContext> : IDeferredToolFunction where TContext : class
{
    // Common fields
    private readonly IServiceProvider _sp;

    private MethodInfo? _methodInfo;
    private Type _pluginType;
    private string? _reflectionBasedName;

    /// <summary>
    /// Private constructor to enforce creation via static factory methods.
    /// </summary>
    public DeferredToolFunction(IServiceProvider sp, Type pluginType, MethodInfo methodInfo, string name)
    {
        _sp = sp;
        _pluginType = pluginType;
        _methodInfo = methodInfo;
        _reflectionBasedName = name;
    }

    public string GetPluginCategory()
    {
        var attribute = _pluginType.GetCustomAttribute<AgentToolPluginAttribute>();
        if (attribute != null && !string.IsNullOrWhiteSpace(attribute.Category))
        {
            return attribute.Category;
        }
        return string.Empty;
    }

    public string GetPluginResourceType()
    {
        var attribute = _pluginType.GetCustomAttribute<AgentToolPluginAttribute>();
        if (attribute != null && !string.IsNullOrWhiteSpace(attribute.ResourceType))
        {
            return attribute.ResourceType;
        }
        return string.Empty;
    }

    public string GetPluginName()
    {
        return _pluginType.Name;
    }

    /// <summary>
    /// Creates the AIFunction based on the source information (Reflection or YAML).
    /// </summary>
    public AIFunction GetToolFunction(Guid? threadId = null)
    {
        // Case 1: The tool was defined by reflecting over source code.
        if (_methodInfo is not null)
        {
            return CreateFromReflection(threadId);
        }

        throw new InvalidOperationException("DeferredToolFunction was not properly initialized. Both reflection and YAML sources are null.");
    }

    private AIFunction CreateFromReflection(Guid? threadId)
    {
        var instance = _sp.GetRequiredService(_pluginType!);

        if (threadId is not null)
        {
            // Note: This logic is preserved from the original implementation.
            // Check for public ThreadId property first
            var threadIdPropertyPublic = _pluginType!.GetProperty("ThreadId", BindingFlags.Instance | BindingFlags.Public);
            if (threadIdPropertyPublic is not null && threadIdPropertyPublic.PropertyType == typeof(Guid?))
            {
                threadIdPropertyPublic.SetValue(instance, threadId);
            }

            // Check all private fields and if they have ThreadId, set it on those objects
            var privateFields = _pluginType.GetFields(BindingFlags.Instance | BindingFlags.NonPublic);
            foreach (var field in privateFields)
            {
                var fieldValue = field.GetValue(instance);
                if (fieldValue != null)
                {
                    var fieldType = fieldValue.GetType();

                    var threadIdProperty = fieldType.GetProperty("ThreadId", BindingFlags.Instance | BindingFlags.Public);
                    if (threadIdProperty != null && threadIdProperty.PropertyType == typeof(Guid?) && threadIdProperty.CanWrite)
                    {
                        threadIdProperty.SetValue(fieldValue, threadId);
                    }

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
            return ContextAIFunction<TContext>.Create(_methodInfo!, contextToolTarget, name: _reflectionBasedName!);
        }

        return AIFunctionFactory.Create(_methodInfo!, instance, name: _reflectionBasedName!);
    }
}

/// <summary>
/// A default implementation of IToolFactory that automatically scans for tools in provided assemblies
/// and can also register tools from external YAML definitions.
/// </summary>
public class ToolFactory<TContext> : IToolFactory<TContext> where TContext : class
{
    private readonly ILogger<ToolFactory<TContext>> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IConfiguration _configuration;
    private readonly IEnumerable<Assembly> _assemblies;
    private readonly Dictionary<string, IDeferredToolFunction> _tools = new();

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

    public void RegisterFromYamlFile(string filePath, BehaviorOnNameConflict onNameConflict = BehaviorOnNameConflict.ThrowException)
    {
        if (!File.Exists(filePath))
        {
            _logger.LogInternalError("YAML tool definition file not found: {filePath}", filePath);
            return;
        }

        _logger.LogInternalInformation("Registering tools from YAML file: {filePath}", filePath);
        var yamlContent = File.ReadAllText(filePath);
        RegisterFromYaml(yamlContent, onNameConflict);
    }

    public void RegisterFromYaml(string yamlContent, BehaviorOnNameConflict onNameConflict = BehaviorOnNameConflict.ThrowException)
    {
        try
        {
            // Step 1: Parse YAML as a generic object and convert it to JSON
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(CamelCaseNamingConvention.Instance)
                .Build();

            var yamlObject = deserializer.Deserialize<object>(yamlContent);
            var json = JsonConvert.SerializeObject(yamlObject);

            // Step 2: Parse JSON and get the tool type
            var jObj = JsonConvert.DeserializeObject<JObject>(json)
                      ?? throw new InvalidOperationException("Failed to parse YAML content to JSON.");

            var typeToken = jObj["type"];
            if (typeToken is null || typeToken.Type != JTokenType.String)
            {
                _logger.LogInternalWarning("Missing or invalid 'type' field in YAML.");
                return;
            }

            var typeName = typeToken.ToString();
            if (!JsonToolConverter.TryResolve(typeName, out var concreteType))
            {
                _logger.LogInternalWarning($"Unknown tool type '{typeName}' in YAML.");
                return;
            }

            // Step 3: Deserialize to concrete type and cast to base type
            var toolDef = (YamlToolDefinitionBase)jObj.ToObject(concreteType)!;

            // Step 4: Handle name conflicts
            if (_tools.ContainsKey(toolDef.Name))
            {
                if (onNameConflict == BehaviorOnNameConflict.ThrowException)
                    throw new InvalidOperationException($"Tool with name '{toolDef.Name}' is already registered.");

                if (onNameConflict == BehaviorOnNameConflict.Ignore)
                    return;

                // Replace on BehaviorOnNameConflict.Overwrite
            }

            // Step 5: Register tool function
            var toolFunction = new YamlToolFunction<TContext>(_serviceProvider, _assemblies, toolDef);
            _tools[toolDef.Name] = toolFunction;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to deserialize or register tool from YAML content.");
        }
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
                    Category = tool.Value.GetToolFunction()?.GetToolCategory(tool.Value.GetPluginCategory()) ?? string.Empty,
                    ResourceType = tool.Value.GetToolFunction()?.GetToolResourceType(tool.Value.GetPluginResourceType()) ?? string.Empty,
                    Description = tool.Value.GetToolFunction()?.Description,
                    PluginName = tool.Value.GetPluginName(),
                    Parameters = tool.Value.GetToolFunction()?.UnderlyingMethod?.GetParameters()?.Select(x => x.Name ?? string.Empty)?.Where(s => !string.IsNullOrEmpty(s)).ToArray()
                });
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to fetch tool info for {toolName}.", tool.Key);
            }
        }
        return result;
    }

    public void FindAndRegisterAllTools(BehaviorOnNameConflict onNameConflict)
    {
        var plugins = _assemblies
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type => type.IsDefined(typeof(AgentToolPluginAttribute)));

        if (!plugins.Any())
        {
            // This is now a warning, as tools can be loaded from other sources like YAML.
            _logger.LogInternalWarning("No tool plugins with [AgentToolPlugin] attribute found in the scanned assemblies.");
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
                throw;
            }
        }
        var toolsYamlDirectory = Path.Combine(AppContext.BaseDirectory, "ToolsV2");
        if (Directory.Exists(toolsYamlDirectory))
        {
            var yamlFiles = Directory.GetFiles(toolsYamlDirectory, "*.yaml", SearchOption.AllDirectories)
                                     .Concat(Directory.GetFiles(toolsYamlDirectory, "*.yml", SearchOption.AllDirectories));

            foreach (var file in yamlFiles)
            {
                RegisterFromYamlFile(file);
            }
        }
    }

    public AIFunction GetTool(string name)
    {
        return DoFindAIFunction(name, null);
    }

    private bool RegisterTool(string name, IDeferredToolFunction function, BehaviorOnNameConflict onNameConflict)
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
            try
            {
                function = deferredToolFunction.GetToolFunction();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to create tool '{functionName}' from its deferred definition.", name);
                function = null;
                return false;
            }
        }

        _logger.LogInternalWarning("Function '{functionName}' not found.", name);
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
            try
            {
                return function.GetToolFunction(threadId);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to create tool '{functionName}' from its deferred definition.", name);
                throw new InvalidOperationException($"Failed to create tool '{name}'. See inner exception for details.", ex);
            }
        }

        _logger.LogInternalError("Function '{functionName}' not found.", name);
        throw new KeyNotFoundException($"Function '{name}' not found.");
    }
}
