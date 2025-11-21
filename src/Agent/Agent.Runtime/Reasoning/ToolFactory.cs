// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core.Configuration;
using Agent.Framework;
using Agent.Framework.Skills;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Tools;
using Agent.Runtime.Interfaces;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
public sealed class ToolFactory<TContext> : AsyncInitializerBase, IToolFactory<TContext>
    where TContext : class
{
    private readonly ILogger<ToolFactory<TContext>> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IEnumerable<Assembly> _assemblies;
    private readonly Dictionary<string, IDeferredToolFunction<TContext>> _tools = new();
    private readonly Dictionary<string, AggregateToolCallPluginDefinitionBase> _aggregatePluginDefinitionsByType = new();
    private readonly HashSet<string> _disabledTools = new();
    private readonly HashSet<string> _extendedTools = new();
    private readonly IExtensibilityLoader? _extensibilityLoader;
    private readonly IMcpConnectable _mcpToolsRepository;
    private readonly ISkillRegistry _skillRegistry;
    private readonly bool _handoffReasoningEnabled;
    private readonly bool _enableAggregateToolFunction;

    public int RegisteredToolCount => _tools.Count;

    public int RegisteredBuiltInToolCount => _tools.Count - _extendedTools.Count;

    public int RegisteredExtendedToolCount => _extendedTools.Count;

    public ToolFactory(
        ILogger<ToolFactory<TContext>> logger,
        IServiceProvider serviceProvider,
        IEnumerable<Assembly> assembliesToScan,
        IExtensibilityLoader? extensibilityLoader,
        IMcpConnectable mcpToolsRepository,
        ISkillRegistry skillRegistry
    )
    {
        _logger = logger;
        _serviceProvider = serviceProvider;
        _assemblies = assembliesToScan;
        _extensibilityLoader = extensibilityLoader;
        _mcpToolsRepository = mcpToolsRepository;
        _skillRegistry = skillRegistry;

        // enable handoff reasoning for dev envs
        var hostEnvironment = _serviceProvider.GetRequiredService<IHostEnvironment>();
        var experimentalSettings = _serviceProvider.GetRequiredService<ExperimentalSettings>();
        _handoffReasoningEnabled = experimentalSettings?.EnableHandoffReasoning ?? hostEnvironment.IsDevelopment();

        // Get aggregate tool function setting from environment variable
        var enableAggregateToolEnv = Environment.GetEnvironmentVariable("ENABLE_AGGREGATE_TOOL_FUNCTION");
        _enableAggregateToolFunction = !string.IsNullOrEmpty(enableAggregateToolEnv) &&
            (enableAggregateToolEnv.Equals("true", StringComparison.OrdinalIgnoreCase) ||
             enableAggregateToolEnv.Equals("1", StringComparison.OrdinalIgnoreCase));
    }

    protected override Task InitializeAsyncCore()
    {
        return FindAndRegisterAllToolsAsync(BehaviorOnNameConflict.Overwrite);
    }

    private void RegisterFromYamlFile(string filePath, BehaviorOnNameConflict onNameConflict = BehaviorOnNameConflict.ThrowException)
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

    private void RegisterFromYaml(string yamlContent, BehaviorOnNameConflict onNameConflict = BehaviorOnNameConflict.ThrowException)
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

    private ToolInfo? BuildToolInfo(string toolName, IDeferredToolFunction<TContext> deferredToolFunction)
    {
        try
        {
            var aiFunction = deferredToolFunction.GetToolFunction();
            var classAttribute = deferredToolFunction.MethodInfo?.DeclaringType?.GetCustomAttribute<AgentToolPluginAttribute>();

            return new ToolInfo
            {
                Name = toolName,
                Category = aiFunction?.GetToolCategory(deferredToolFunction.GetPluginCategory()) ?? string.Empty,
                ResourceType = aiFunction?.GetToolResourceType(deferredToolFunction.GetPluginResourceType()) ?? string.Empty,
                Description = aiFunction?.Description,
                PluginName = deferredToolFunction.GetPluginName(),
                Parameters = aiFunction?.UnderlyingMethod?.GetParameters()?.Select(p => p.Name)?.Where(n => !string.IsNullOrWhiteSpace(n)).ToList() ?? new List<string?>(),
                IsIncidentHandlerTool = classAttribute?.IsIncidentHandlerPlugin ?? false,
                IncidentHandlerPlatform = classAttribute?.IncidentPlatform.ToString() ?? string.Empty,
                IsMcp = aiFunction?.IsMcpTool() ?? false
            };
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to build ToolInfo for {ToolName}", toolName);
            return null; // do not add entry
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
                var info = BuildToolInfo(tool.Key, tool.Value);
                if (info != null)
                {
                    result.Add(info);
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to fetch tool info for {toolName}.", tool.Key);
                // skip adding entry
            }
        }
        return result;
    }

    public List<ToolInfo> FetchToolInfoForToolNames(List<string> toolNames)
    {
        return _tools.Where(tool => toolNames.Contains(tool.Key))
                     .Select(tool => BuildToolInfo(tool.Key, tool.Value))
                     .Where(info => info != null)
                     .Select(info => info!)
                     .ToList();
    }

    public async Task FindAndRegisterAllToolsAsync(BehaviorOnNameConflict onNameConflict)
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

                // Check EnabledIf condition to determine if tools should be disabled
                var isPluginEnabled = IsConditionMet(attribute.EnabledIf);

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

                // Cache aggregate plugin definitions (only if aggregate function is enabled)
                // This needs to happen before we register the methods so they can be wrapped in AggregateDeferredToolFunction
                var isAggregatePlugin = _enableAggregateToolFunction &&
                                          typeof(AggregateToolCallPluginDefinitionBase).IsAssignableFrom(pluginType);
                if (isAggregatePlugin)
                {
                    // Get or create the instance and cache it by aggregate type
                    var aggregateInstance = (AggregateToolCallPluginDefinitionBase)_serviceProvider.GetRequiredService(pluginType);
                    // Get the aggregate type from the instance property
                    var aggregateTypeProperty = pluginType.GetProperty("AggregateType", BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Public);
                    var aggregateType = aggregateTypeProperty?.GetValue(aggregateInstance) as string;

                    if (!string.IsNullOrEmpty(aggregateType))
                    {
                        _aggregatePluginDefinitionsByType[aggregateType] = aggregateInstance;
                        _logger.LogInternalInformation("Cached aggregate plugin definition for type: {aggregateType}", aggregateType);
                    }
                    // Continue to register the methods below (the aggregate function itself)
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

                    // Create AggregateDeferredToolFunction if this plugin is an aggregate plugin definition
                    IDeferredToolFunction<TContext> tool;


                    if (isAggregatePlugin)
                    {
                        tool = new AggregateDeferredToolFunction<TContext>(
                            _serviceProvider,
                            pluginType,
                            method,
                            functionName);
                    }
                    else
                    {
                        tool = new DeferredToolFunction<TContext>(_serviceProvider, pluginType, method, functionName);
                    }

                    if (!RegisterTool(functionName, tool, onNameConflict))
                    {
                        _logger.LogInternalWarning("Failed to register tool {functionName} from type {pluginType} due to name conflict.", functionName, pluginType.FullName);
                    }
                    else if (!isPluginEnabled)
                    {
                        // Track disabled tools
                        _disabledTools.Add(functionName);
                        _logger.LogInternalInformation("Tool {functionName} from plugin {pluginType} is disabled due to EnabledIf condition: {enabledIf}",
                            functionName, pluginType.Name, attribute.EnabledIf);
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
                if (RegisterTool(tool, onNameConflict))
                {
                    _extendedTools.Add(tool.Name);
                }
            }
        }

        // Register the ToDo Write tool
        RegisterTool(ToDoWriteTool<TContext>.GetInstance(), onNameConflict);

        // Register skills as tools if enabled
        RegisterTool(
            ReadSkillFileTool<TContext>.ToolName,
            new AIDynamicTool<TContext>((threadId, agentMode, agent) => new ReadSkillFileTool<TContext>(_skillRegistry, agent)),
            onNameConflict);

        if (_mcpToolsRepository != null)
        {
            _logger.LogInternalInformation("Initializing MCP tools repository...");
            await _mcpToolsRepository.InitializeAsync();

            var mcpFunctions = _mcpToolsRepository.GetAllFunctions();
            _logger.LogInternalInformation("MCP tools repository initialized. Found {Count} MCP tools", mcpFunctions.Count);

            foreach (var aiFunction in mcpFunctions)
            {
                RegisterTool(aiFunction, onNameConflict);
            }

            _logger.LogInternalInformation("Registered {Count} MCP tools in ToolFactory", mcpFunctions.Count);
        }
        else
        {
            _logger.LogInternalInformation("MCP tools repository is null. No MCP tools will be loaded.");
        }
    }

    public AIFunction GetTool(string name, Agent<TContext>? agent = null)
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

        var toolFunction = new YamlToolFunction<TContext>(_serviceProvider, _assemblies, tool);
        return RegisterTool(tool.Name, toolFunction, onNameConflict);
    }

    public bool RegisterTool(AIFunction function, BehaviorOnNameConflict onNameConflict)
    {
        var deferredFunction = new AIToolAdapter<TContext>(function);
        return RegisterTool(function.Name, deferredFunction, onNameConflict);
    }

    public bool RegisterTool(string name, IDeferredToolFunction<TContext> function, BehaviorOnNameConflict onNameConflict)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            _logger.LogInternalError("Function name cannot be null or whitespace.");
            return false;
        }

        // Check if this is a YamlToolFunction with an aggregate type and if aggregate function is enabled
        if (_enableAggregateToolFunction && function is YamlToolFunction<TContext> yamlFunction)
        {
            var aggregateType = yamlFunction.GetAggregateToolType(); // Tool type for YAML tools

            if (!string.IsNullOrEmpty(aggregateType) &&
                _aggregatePluginDefinitionsByType.TryGetValue(aggregateType, out var aggregateDefinition))
            {
                // Register with the aggregate plugin definition instead of _tools
                aggregateDefinition.RegisterTool(aggregateType, name, function);
                _logger.LogInternalInformation("Function '{functionName}' registered with aggregate plugin for type '{aggregateType}'.", name, aggregateType);

                // Update the aggregate tool's description with the new aggregated description
                UpdateAggregateToolDescription(aggregateType, aggregateDefinition);

                return true;
            }
        }

        // Normal registration for non-aggregate tools
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

    /// <summary>
    /// Updates the description of the aggregate tool function with the aggregated descriptions of all registered tools.
    /// </summary>
    private void UpdateAggregateToolDescription(string aggregateType, AggregateToolCallPluginDefinitionBase aggregateDefinition)
    {
        try
        {
            // Get the aggregated description from the plugin
            var aggregatedDescription = aggregateDefinition.GetAggregatedDescription();

            if (string.IsNullOrEmpty(aggregatedDescription))
            {
                return;
            }

            // Find the AggregateDeferredToolFunction for this aggregate type in _tools
            var aggregateDefinitionType = aggregateDefinition.GetType();
            var aggregateToolEntry = _tools.FirstOrDefault(t =>
                t.Value is AggregateDeferredToolFunction<TContext> aggregateTool &&
                aggregateTool.MethodInfo?.DeclaringType == aggregateDefinitionType);

            if (aggregateToolEntry.Value is AggregateDeferredToolFunction<TContext> aggregateFunction)
            {
                // Update the aggregate function's description with the aggregated description
                aggregateFunction.UpdateDescription(aggregatedDescription);
                _logger.LogInternalInformation("Updated description for aggregate tool '{toolName}' with {aggregateType} tools.", aggregateToolEntry.Key, aggregateType);
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to update aggregate tool description for type '{aggregateType}'.", aggregateType);
        }
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

            // Mark as extended tool
            _extendedTools.Add(extendedToolName);

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

    public async Task RefreshMcpToolsAsync()
    {
        _logger.LogInternalInformation("Refreshing MCP tools from repository");

        try
        {
            // Get all MCP tools from the repository
            var mcpFunctions = _mcpToolsRepository.GetAllFunctions();

            // Register each MCP tool using AIToolAdapter to wrap the AIFunction
            foreach (var function in mcpFunctions)
            {
                var toolAdapter = new AIToolAdapter<TContext>(function);
                _tools[function.Name] = toolAdapter;

                _logger.LogInternalDebug("Registered MCP tool: {ToolName}", function.Name);
            }

            _logger.LogInternalInformation("Refreshed {Count} MCP tools", mcpFunctions.Count);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to refresh MCP tools");
            throw;
        }

        await Task.CompletedTask;
    }

    public bool IsToolDisabled(string name)
    {
        return _disabledTools.Contains(name);
    }

    private bool IsConditionMet(string condition)
    {
        if (string.IsNullOrEmpty(condition))
        {
            return true;
        }

        var parts = condition.Split(':', 2);
        if (parts.Length != 2)
        {
            _logger.LogInternalWarning("Invalid EnabledIf condition format: {condition}", condition);
            return false;
        }

        var conditionType = parts[0];
        var expectedValue = parts[1];

        // Check if it's a DataConnectorType condition
        if (conditionType.Equals("DataConnectorType", StringComparison.OrdinalIgnoreCase))
        {
            var coreSettings = _serviceProvider.GetService<CoreSettings>();
            if (coreSettings == null)
            {
                _logger.LogInternalWarning("CoreSettings not found in service provider for DataConnectorType condition check");
                return false;
            }

            var hasConnector = coreSettings.DataConnectors
                .Any(dc => string.Equals(dc.DataConnectorType, expectedValue, StringComparison.OrdinalIgnoreCase));

            return hasConnector;
        }

        // Default: treat as environment variable check
        var actualValue = Environment.GetEnvironmentVariable(conditionType);
        return string.Equals(actualValue, expectedValue, StringComparison.OrdinalIgnoreCase);
    }

    public AIFunction GetTool(string name, Guid threadId, Agent<TContext>? agent = null)
    {
        return DoFindAIFunction(name, threadId, agent: agent);
    }

    public AIFunction GetTool(string name, Guid threadId, string? agentMode, Agent<TContext>? agent = null)
    {
        return DoFindAIFunction(name, threadId, agentMode, agent: agent);
    }

    private AIFunction DoFindAIFunction(string name, Guid? threadId = null, string? agentMode = null, Agent<TContext>? agent = null)
    {
        if (_tools.TryGetValue(name, out var function))
        {
            try
            {
                return function.GetToolFunction(threadId, agentMode, agent);
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
