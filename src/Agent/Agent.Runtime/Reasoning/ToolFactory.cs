// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Framework;
using Agent.Plugins;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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

    /// <summary>
    /// This is called to load custom tools defined in extensible agents repo
    /// </summary>
    /// <param name="customAgentFiles"></param>
    public void FindAndRegisterCustomTools(CustomAgentFiles customAgentFiles)
    {
        if (customAgentFiles?.kql == null || !customAgentFiles.kql.Any())
        {
            _logger.LogInternalInformation("No KQL files found to register as custom tools.");
            return;
        }

        _logger.LogInternalInformation("Registering {count} KQL files as custom tools", customAgentFiles.kql.Count);

        // Just register the queries with the plugin, don't create individual tools
        // The plugin will be discovered and registered by FindAndRegisterAllTools
        var kqlToolsPlugin = _serviceProvider.GetRequiredService<DynamicKqlToolsPlugin>();
        var kqlToolsPluginType = typeof(DynamicKqlToolsPlugin);

        // Get the ExecuteDynamicQueryByName method from the TYPE, not the instance
        var executeMethod = kqlToolsPluginType.GetMethod("ExecuteDynamicQueryByName", BindingFlags.Public | BindingFlags.Instance);

        var customAgentDatabase = string.Empty;
        var customAgentcluster = string.Empty;
        var appSettings = customAgentFiles.appsettings?.FirstOrDefault().Value;
        if (appSettings != null)
        {
            try
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(appSettings));
                var customAppSettings = doc.RootElement.GetProperty("AppSettings").Deserialize<AppSettings>();

                var kustoSettings = customAppSettings?.Core.External.Kusto;

                foreach (var kqlFile in customAgentFiles.kql)
                {
                    try
                    {
                        string queryName = Path.GetFileNameWithoutExtension(kqlFile.Key);
                        string queryContent = File.ReadAllText(kqlFile.Value);

                        // Parse cluster and database from the KQL file
                        var (cluster, database, cleanedQuery, region, description) = ParseKqlFile(queryContent);

                        if (string.IsNullOrEmpty(cluster) || string.IsNullOrEmpty(database) || string.IsNullOrEmpty(region))
                        {
                            customAgentDatabase = kustoSettings.RegionalClusterGroups
                                .Select(db => db.Name.ToLower()).FirstOrDefault();
                            customAgentcluster = string.IsNullOrEmpty(cluster)
                                ? kustoSettings.RegionalClusterGroups.FirstOrDefault()?.Regions.FirstOrDefault(
                                    r => r.Region.Equals("westeurope", StringComparison.InvariantCultureIgnoreCase))?.ClusterUri
                                : kustoSettings.RegionalClusterGroups.FirstOrDefault()?.Regions.FirstOrDefault(
                                    r => r.Region.Equals(region, StringComparison.InvariantCultureIgnoreCase))?.ClusterUri;
                        }
                        else
                        {
                            _logger.LogInternalWarning("Could not parse cluster and database from KQL file '{filePath}'. Using defaults.", kqlFile.Value);
                            cluster = customAgentcluster;
                            database = customAgentDatabase;
                        }

                        // Just register the query with the plugin - no individual tool registration needed
                        kqlToolsPlugin.RegisterQuery(queryName, customAgentcluster, customAgentDatabase, cleanedQuery, description);

                        // Also register each KQL file name as an individual tool
                        if (executeMethod != null)
                        {
                            var tool = new DeferredToolFunction<TContext>(_serviceProvider, kqlToolsPluginType, executeMethod, queryName);
                            RegisterTool(queryName, tool, BehaviorOnNameConflict.Ignore);
                        }

                        _logger.LogInternalInformation("Registered KQL query '{queryName}' with plugin from file '{filePath}' for cluster '{cluster}' and database '{database}'",
                            queryName, kqlFile.Value, customAgentcluster, customAgentDatabase);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, "Failed to register KQL file '{filePath}' with plugin", kqlFile.Value);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to parse appsettings file '{filePath}' for custom agent cluster and database.", appSettings);
                // Fallback to empty strings if parsing fails
            }
        }
    }

    // Update the ParseKqlFile method to also extract description
    private (string cluster, string database, string cleanedQuery, string region, string description) ParseKqlFile(string queryContent)
    {
        string cluster = string.Empty;
        string database = string.Empty;
        string description = string.Empty;
        string region = string.Empty;
        var lines = queryContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        var cleanedLines = new List<string>();

        foreach (var line in lines)
        {
            var trimmedLine = line.Trim();

            // Look for metadata in comments
            if (trimmedLine.StartsWith("//"))
            {
                var commentContent = trimmedLine.Substring(2).Trim();

                if (commentContent.StartsWith("cluster:", StringComparison.OrdinalIgnoreCase))
                {
                    cluster = commentContent.Substring(8).Trim();
                    continue;
                }
                else if (commentContent.StartsWith("database:", StringComparison.OrdinalIgnoreCase))
                {
                    database = commentContent.Substring(9).Trim();
                    continue;
                }
                else if (commentContent.StartsWith("region:", StringComparison.OrdinalIgnoreCase))
                {
                    region = commentContent.Substring(7).Trim();
                    continue;
                }
                else if (commentContent.StartsWith("description:", StringComparison.OrdinalIgnoreCase))
                {
                    description = commentContent.Substring(12).Trim();
                    continue;
                }
            }

            // Add non-metadata lines to the cleaned query
            cleanedLines.Add(line);
        }

        var cleanedQuery = string.Join('\n', cleanedLines);
        return (cluster, database, cleanedQuery, region, description);
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
                    Category = tool.Value.GetToolFunction()?.GetToolCategory(tool.Value.GetPluginCategory()),
                    ResourceType = tool.Value.GetToolFunction()?.GetToolResourceType(tool.Value.GetPluginResourceType()),
                    Description = tool.Value.GetToolFunction()?.Description,
                    PluginName = tool.Value.GetPluginName(),
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
        // AME : 33e01921-4d64-4f8c-a055-5bdaffd5e33d
        // CORP : 72f988bf-86f1-41af-91ab-2d7cd011db47
        // PME : 975f013f-7f24-47e8-a7d3-abc4752bf346
        // TORUS : cdc5aeea-15c5-4db6-b079-fcadd2505dc2	
        var firstPartyTenants = new List<string>() { "33e01921-4d64-4f8c-a055-5bdaffd5e33d", "72f988bf-86f1-41af-91ab-2d7cd011db47", "975f013f-7f24-47e8-a7d3-abc4752bf346", "cdc5aeea-15c5-4db6-b079-fcadd2505dc2" };

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

                // Temp: Load all 1P tools to unblock 3p agents start up
                //if (!ShouldRegisterPlugin(pluginName: pluginType.Name, attribute))
                //{
                //    _logger.LogInternalInformation("Skipping registration of plugin {pluginName} due to attribute conditions.", pluginType.Name);
                //    continue;
                //}

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
