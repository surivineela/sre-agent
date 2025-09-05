// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Framework.Interfaces;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Tools;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Runtime.Reasoning;

/// <summary>
/// A default implementation of IToolFactory that automatically scans for tools in provided assemblies
/// and can also register tools from external YAML definitions.
/// </summary>
public class ToolFactory<TContext> : IToolFactory<TContext> where TContext : class
{
    private readonly ILogger<ToolFactory<TContext>> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly IEnumerable<Assembly> _assemblies;
    private readonly Dictionary<string, IDeferredToolFunction> _tools = new();
    
    private readonly bool _handoffReasoningEnabled;
    private readonly IExtensibilityLoader? _extensibilityLoader;
    public ToolFactory(
        ILogger<ToolFactory<TContext>> logger,
        IServiceProvider serviceProvider,
        IEnumerable<Assembly> assembliesToScan,
        IExtensibilityLoader? extensibilityLoader = null
    )
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _assemblies = assembliesToScan;
        _hostEnvironment = _serviceProvider.GetRequiredService<IHostEnvironment>();
        _extensibilityLoader = extensibilityLoader;

        // enable handoff reasoning for dev envs
        var experimentalSettings = _serviceProvider.GetRequiredService<ExperimentalSettings>();
        _handoffReasoningEnabled = experimentalSettings?.EnableHandoffReasoning ?? _hostEnvironment.IsDevelopment();

        
        FindAndRegisterAllTools(BehaviorOnNameConflict.Overwrite);
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
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
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
            if (!JsonPolymorphicToolConverter.TryResolve(typeName, out var concreteType))
            {
                _logger.LogInternalWarning($"Unknown tool type '{typeName}' in YAML.");
                return;
            }

            // Step 3: Deserialize to concrete type and cast to base type
            var jsonSerializer = JsonSerializer.Create(new JsonSerializerSettings
            {
                ContractResolver = new DefaultContractResolver
                {
                    NamingStrategy = new SnakeCaseNamingStrategy()
                }
            });

            var toolDefObject = jObj.ToObject(concreteType, jsonSerializer);
            if (toolDefObject is null)
            {
                _logger.LogInternalWarning("Failed to deserialize YAML content to tool definition.");
                return;
            }

            var toolDef = (YamlToolDefinitionBase)toolDefObject;

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

    public List<ToolInfo> FetchAvailableToolInfo(Func<MethodInfo, bool>? filter = null)
    {
        var result = new List<ToolInfo>();
        foreach (var tool in _tools)
        {
            try
            {
                if (filter is not null && tool.Value.MethodInfo is not null && !filter(tool.Value.MethodInfo))
                {
                    continue;
                }

                // Check the class-level AgentToolPluginAttribute for incident handler info
                var classAttribute = tool.Value.MethodInfo?.DeclaringType?.GetCustomAttribute<AgentToolPluginAttribute>();

                result.Add(new ToolInfo
                {
                    Name = tool.Key,
                    Category = tool.Value.GetToolFunction()?.GetToolCategory(tool.Value.GetPluginCategory()) ?? string.Empty,
                    ResourceType = tool.Value.GetToolFunction()?.GetToolResourceType(tool.Value.GetPluginResourceType()) ?? string.Empty,
                    Description = tool.Value.GetToolFunction()?.Description,
                    PluginName = tool.Value.GetPluginName(),
                    Parameters = tool.Value.GetToolFunction()?.UnderlyingMethod?.GetParameters()?.Select(x => x.Name ?? string.Empty)?.Where(s => !string.IsNullOrEmpty(s)).ToArray() ?? [],
                    // Use class-level attribute for incident handler info
                    IsIncidentHandlerTool = classAttribute?.IsIncidentHandlerPlugin ?? false,
                    IncidentHandlerPlatform = classAttribute?.IncidentPlatform.ToString() ?? string.Empty
                });
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to fetch tool info for {toolName}.", tool.Key);
            }
        }
        return result;
    }

    public List<ToolInfo> FetchToolInfoForToolNames(List<string> toolNames)
    {
        return [.. _tools.Where(kv => toolNames.Contains(kv.Key)).Select(tool =>
        {
            // Check the class-level AgentToolPluginAttribute for incident handler info
            var classAttribute = tool.Value.MethodInfo?.DeclaringType?.GetCustomAttribute<AgentToolPluginAttribute>();
            return new ToolInfo
            {
                Name = tool.Key,
                Category = tool.Value.GetToolFunction()?.GetToolCategory(tool.Value.GetPluginCategory()) ?? string.Empty,
                ResourceType = tool.Value.GetToolFunction()?.GetToolResourceType(tool.Value.GetPluginResourceType()) ?? string.Empty,
                Description = tool.Value.GetToolFunction()?.Description,
                PluginName = tool.Value.GetPluginName(),
                Parameters = tool.Value.GetToolFunction()?.UnderlyingMethod?.GetParameters()?.Select(x => x.Name)?.ToList() ?? [],
                // Use class-level attribute for incident handler info
                IsIncidentHandlerTool = classAttribute?.IsIncidentHandlerPlugin ?? false,
                IncidentHandlerPlatform = classAttribute?.IncidentPlatform.ToString() ?? string.Empty
            };
        })];
    }

    public async void FindAndRegisterAllTools(BehaviorOnNameConflict onNameConflict)
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

                // ignore disabled plugins
                if (!attribute.IsEnabled)
                {
                    continue;
                }

                // if handoff reasoning enabled, ignore AgentControlFlowPluginDefinition
                if (_handoffReasoningEnabled
                    && pluginType == typeof(AgentControlFlowPluginDefinition))
                {
                    continue;
                }

                // and vice versa, if handoff reasoning disabled, ignore AgentReasoningControlFlowPluginDefinition
                if (!_handoffReasoningEnabled
                    && pluginType == typeof(AgentReasoningControlFlowPluginDefinition))
                {
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
        if (_extensibilityLoader != null)
        {
            var extendedTools = await _extensibilityLoader.LoadExtendedToolsAsync();
            foreach (var tool in extendedTools)
            {
                RegisterTool(tool, onNameConflict);
            }

        }

        // Register the ToDo Write tool
        RegisterTool(ToDoWriteTool.ToolName, ToDoWriteTool.Instance, onNameConflict);

    }

    public AIFunction GetTool(string name)
    {
        return DoFindAIFunction(name, null);
    }

    public bool RegisterTool(YamlToolDefinitionBase tool, BehaviorOnNameConflict onNameConflict)
    {

        if (string.IsNullOrWhiteSpace(tool.Name))
        {
            _logger.LogInternalError("Function name cannot be null or whitespace.");
            return false;
        }

        if (_tools.ContainsKey(tool.Name))
        {
            switch (onNameConflict)
            {
                case BehaviorOnNameConflict.ThrowException:
                    throw new InvalidOperationException($"Function '{tool.Name}' already exists.");
                case BehaviorOnNameConflict.Ignore:
                    _logger.LogInternalWarning("Function '{functionName}' already exists. Ignoring the new function.", tool.Name);
                    return false;

                case BehaviorOnNameConflict.Overwrite:
                    _logger.LogInternalWarning("Function '{functionName}' already exists. Overwriting the existing function.", tool.Name);
                    break;
            }
        }

        var toolFunction = new YamlToolFunction<TContext>(_serviceProvider, _assemblies, tool);
        _tools[tool.Name] = toolFunction;

        _logger.LogInternalInformation("Function '{functionName}' registered successfully.", tool.Name);
        return true;
    }

    public bool RegisterTool(string name, IDeferredToolFunction function, BehaviorOnNameConflict onNameConflict)
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

   

    public void RegisterExtendedToolFromModel(string extendedToolName, string extendedToolYaml)
    {
        try
        {
            // Register using existing YAML registration logic
            RegisterFromYaml(extendedToolYaml, BehaviorOnNameConflict.Overwrite);

            _logger.LogInternalDebug("Successfully registered extended tool {ToolName} from Cosmos DB", extendedToolName);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to register extended tool {ToolName} from model", extendedToolName);
            throw;
        }
    }

    public bool HasTool(string name)
    {
        return _tools.ContainsKey(name);
    }

    public AIFunction GetTool(string name, Guid threadId)
    {
        return DoFindAIFunction(name, threadId);
    }

    public AIFunction GetTool(string name, Guid threadId, string? agentMode)
    {
        return DoFindAIFunction(name, threadId, agentMode);
    }

    private AIFunction DoFindAIFunction(string name, Guid? threadId = null, string? agentMode = null)
    {
        if (_tools.TryGetValue(name, out var function))
        {
            try
            {
                return function.GetToolFunction(threadId, agentMode);
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
