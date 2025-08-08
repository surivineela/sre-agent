// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Attributes;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Tools;
using Agent.Web.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
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

    public MethodInfo? MethodInfo => _methodInfo;

    /// <summary>
    /// Creates the AIFunction based on the source information (Reflection or YAML).
    /// </summary>
    public AIFunction GetToolFunction(Guid? threadId = null)
    {
        return GetToolFunction(threadId, null);
    }

    /// <summary>
    /// Creates the AIFunction based on the source information (Reflection or YAML) with agent mode support.
    /// </summary>
    public AIFunction GetToolFunction(Guid? threadId, string? agentMode)
    {
        // Case 1: The tool was defined by reflecting over source code.
        if (_methodInfo is not null)
        {
            return CreateFromReflection(threadId, agentMode);
        }

        throw new InvalidOperationException("DeferredToolFunction was not properly initialized. Both reflection and YAML sources are null.");
    }

    private AIFunction CreateFromReflection(Guid? threadId, string? agentMode)
    {
        var instance = _sp.GetRequiredService(_pluginType!);

        // Check if this is a write operation in read-only mode
        var writeActionAttr = _methodInfo!.GetCustomAttribute<WriteActionAttribute>();
        var isReadOnlyMode = IsReadOnlyMode(agentMode);

        if (writeActionAttr != null && isReadOnlyMode && !writeActionAttr.RunInReadOnlyMode)
        {
            // Create a wrapper that returns mock responses instead of executing
            return CreateReadOnlyMockFunction(instance, threadId, writeActionAttr.ReadOnlyMessage);
        }

        // Standard tool creation logic (existing code continues here...)
        if (threadId is not null)
        {
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

    private bool IsReadOnlyMode(string? agentMode)
    {
        return string.Equals(agentMode, ActionMode.ReadOnly.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    private AIFunction CreateReadOnlyMockFunction(object instance, Guid? threadId, string? customMessage)
    {
        var logger = _sp.GetService<ILogger<DeferredToolFunction<TContext>>>();
        return new ReadOnlyMockFunction(_methodInfo!, instance, _reflectionBasedName!, logger, customMessage);
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
    private readonly IExtendedAgentRepository? _extendedAgentRepository;
    private readonly bool _handoffReasoningEnabled;

    public ToolFactory(
        ILogger<ToolFactory<TContext>> logger,
        IServiceProvider serviceProvider,
        IEnumerable<Assembly> assembliesToScan,
        IExtendedAgentRepository? extendedAgentRepository = null
    )
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _assemblies = assembliesToScan;
        _hostEnvironment = _serviceProvider.GetRequiredService<IHostEnvironment>();
        _configuration = _serviceProvider.GetRequiredService<IConfiguration>();

        var experimentalSettings = _configuration.GetSection("AppSettings:Core:Experimental").Get<ExperimentalSettings>();
        _handoffReasoningEnabled = experimentalSettings?.EnableHandoffReasoning ?? false;

        _extendedAgentRepository = extendedAgentRepository;
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

                result.Add(new ToolInfo
                {
                    Name = tool.Key,
                    Category = tool.Value.GetToolFunction()?.GetToolCategory(tool.Value.GetPluginCategory()) ?? string.Empty,
                    ResourceType = tool.Value.GetToolFunction()?.GetToolResourceType(tool.Value.GetPluginResourceType()) ?? string.Empty,
                    Description = tool.Value.GetToolFunction()?.Description,
                    PluginName = tool.Value.GetPluginName(),
                    Parameters = tool.Value.GetToolFunction()?.UnderlyingMethod?.GetParameters()?.Select(x => x.Name ?? string.Empty)?.Where(s => !string.IsNullOrEmpty(s)).ToArray() ?? []
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
            return new ToolInfo
            {
                Name = tool.Key,
                Category = tool.Value.GetToolFunction()?.GetToolCategory(tool.Value.GetPluginCategory()) ?? string.Empty,
                ResourceType = tool.Value.GetToolFunction()?.GetToolResourceType(tool.Value.GetPluginResourceType()) ?? string.Empty,
                Description = tool.Value.GetToolFunction()?.Description,
                PluginName = tool.Value.GetPluginName(),
                Parameters = tool.Value.GetToolFunction()?.UnderlyingMethod?.GetParameters()?.Select(x => x.Name)?.ToList() ?? []
            };
        })];
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

    /// <summary>
    /// Loads extended tools from Cosmos DB during initialization
    /// </summary>
    private void LoadExtendedToolsFromCosmos()
    {
        //if (_extendedAgentRepository == null)
        //{
        //    _logger.LogInternalDebug("ExtendedAgentRepository is not available. Skipping extended tools from Cosmos DB.");
        //    return;
        //}

        try
        {
            _logger.LogInternalInformation("Loading extended tools from Cosmos DB...");

            // Load all extended tools synchronously during initialization
            //var extendedTools = _extendedAgentRepository.GetToolsAsync(limit: 1000).GetAwaiter().GetResult();

            //foreach (var extendedTool in extendedTools)
            //{
            //    try
            //    {
            //       // RegisterExtendedToolFromModel(extendedTool.Name, extendedTool.ToYaml());
            //    }
            //    catch (Exception ex)
            //    {
            //        _logger.LogInternalError(ex, "Failed to load extended tool {ToolName} from Cosmos DB during initialization", extendedTool.Name);
            //        // Continue loading other tools even if one fails
            //    }
            //}

            //_logger.LogInternalInformation("Successfully loaded {Count} extended tools from Cosmos DB", extendedTools.Count);
        }
        catch (Exception ex)
        {
            _logger.LogInternalWarning(ex, "Failed to load extended tools from Cosmos DB during initialization. Continuing without them.");
        }
    }

    // In LoadExtendedToolsFromCosmosOnDemandAsync, fix possible null reference for connectorDocument
    public async Task LoadExtendedToolsFromCosmosOnDemandAsync()
    {
        if (_extendedAgentRepository == null)
        {
            _logger.LogInternalWarning("ExtendedAgentRepository is not available. Cannot load extended tools on demand.");
            return;
        }

        try
        {
            _logger.LogInternalInformation("Loading extended tools from Cosmos DB on demand...");

            // Load all extended tools
            var extendedTools = await _extendedAgentRepository.GetToolsAsync(limit: 1000);

            // Load new ones
            foreach (var extendedTool in extendedTools)
            {
                try
                {
                    var concretetool = DocumentToRuntimeMapper.ToRuntimeTool(extendedTool);
                    if (concretetool.Connector != null)
                    {
                        var connectorDocument = await _extendedAgentRepository.GetConnectorByNameAsync(concretetool.Connector);
                        if (connectorDocument != null)
                        {
                            concretetool.ConnectorData = DocumentToRuntimeMapper.ToRuntimeConnector(connectorDocument);
                        }
                    }
                    RegisterTool(concretetool, BehaviorOnNameConflict.Overwrite);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load extended tool {ToolName} from Cosmos DB on demand", extendedTool.Name);
                    // Continue loading other tools even if one fails
                }
            }

            _logger.LogInternalInformation("Successfully loaded {Count} extended tools from Cosmos DB on demand", extendedTools.Count);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to load extended tools from Cosmos DB on demand");
            throw;
        }
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
