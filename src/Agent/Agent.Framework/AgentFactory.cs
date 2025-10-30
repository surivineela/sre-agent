// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Framework;

/// <summary>
/// Type of change that occurred to an agent
/// </summary>
public enum AgentChangeType
{
    Added,
    Updated,
    Removed
}

/// <summary>
/// Event args for agent changes
/// </summary>
public class AgentChangedEventArgs : EventArgs
{
    public string AgentName { get; }
    public AgentChangeType ChangeType { get; }

    public AgentChangedEventArgs(string agentName, AgentChangeType changeType)
    {
        AgentName = agentName;
        ChangeType = changeType;
    }
}

public interface IAgentFactory<TContext> : IAsyncInitializer
    where TContext : class
{
    public Agent<TContext> GetAgent(string name);

    public IReadOnlyDictionary<string, IPromptDescriptor> PromptDescriptors { get; }

    public int RegisteredAgentCount { get; }

    public Agent<TContext> LoadAgentFromYamlContent(string yamlContent, bool isCustomAgent);

    public Agent<TContext> LoadAgentFromDescriptor(YamlAgentDescriptor yamlContent, bool isCustomAgent);

    // Overwrite existing agent agents, useful for loading agents with different prompts when some feature flags are enabled, e.g agent memory RAG
    public void LoadYamlAgentsFromFolder(string folderPath, bool overwriteExistingAgents, bool recursive);

    public void LoadCommonPromptFromDescriptor(YamlPromptDescriptor prompt);

    public void LoadCommonToolsListFromDescriptor(YamlCommonToolsDescriptor toolsList);

    void UpdateHandoffs();

    List<IAgentDescriptor> GetAllAgentDescriptors();

    IReadOnlyList<Experiment> Experiments { get; }
}

public sealed class AgentFactory<TContext> : AsyncInitializerBase, IAgentFactory<TContext>
    where TContext : class
{
    // A map from Agent name -> Agent descriptor
    private readonly Dictionary<string, Agent<TContext>> _agents = [];

    private readonly Dictionary<string, IAgentDescriptor> _agentDescriptors = [];
    private readonly Dictionary<string, IPromptDescriptor> _promptDescriptors = [];
    private readonly Dictionary<string, List<string>> _commonToolsDescriptors = [];
    private readonly ILogger<AgentFactory<TContext>> _logger;
    private readonly IToolFactory<TContext> _toolFactory;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly IEnumerable<Assembly> _assembliesToScan;
    private readonly string? _agentsYamlDirectory;
    private readonly string? _commonPromptsYamlDirectory;
    private readonly string? _commonToolsYamlDirectory;
    private readonly string? _experimentsYamlDirectory;
    private readonly IEnumerable<string>? _promptStarters;
    private readonly IEnumerable<string>? _promptEnders;
    private readonly Type? _defaultOutputType;
    private readonly IAgentModeConfigurator<TContext> _modeConfigurator;
    private readonly bool _enableHandoffReasoning;
    private readonly IExtensibilityLoader? _extensibiltyLoader;

    /// <summary>
    /// Gets whether handoff reasoning is enabled for agents created by this factory.
    /// Exposed for use by AgentProvider.
    /// </summary>
    public bool EnableHandoffReasoning => _enableHandoffReasoning;
    private readonly bool _gpt5Enabled;
    private readonly bool _agentMemoryRetrievalEnabled;
    private readonly bool _scheduledTasksEnabled;
    private readonly List<Experiment> _experiments = [];

    /// <summary>
    /// Event raised when an agent is added, updated, or removed from the factory
    /// </summary>
    public event EventHandler<AgentChangedEventArgs>? AgentChanged;

    public int RegisteredAgentCount => _agents.Count;

    public AgentFactory(
        ILogger<AgentFactory<TContext>> logger,
        IToolFactory<TContext> toolFactory,
        IChatClientProvider chatClientProvider,
        IEnumerable<Assembly> assembliesToScan,
        IAgentModeConfigurator<TContext> modeConfigurator,
        string? agentsYamlDirectory = null,
        string? commonPromptsYamlDirectory = null,
        string? commonToolsYamlDirectory = null,
        string? experimentsYamlDirectory = null,
        IEnumerable<string>? promptStarters = null,
        IEnumerable<string>? promptEnders = null,
        Type? defaultOutputType = null,
        bool enableHandoffReasoning = false,
        IExtensibilityLoader? extensibiltyLoader = null,
        bool gpt5Enabled = false,
        bool agentMemoryRetrievalEnabled = false,
        bool scheduledTasksEnabled = false
    )
    {
        _toolFactory = toolFactory;
        _chatClientProvider = chatClientProvider;
        _logger = logger;
        _assembliesToScan = assembliesToScan;
        _agentsYamlDirectory = agentsYamlDirectory;
        _commonPromptsYamlDirectory = commonPromptsYamlDirectory;
        _commonToolsYamlDirectory = commonToolsYamlDirectory;
        _experimentsYamlDirectory = experimentsYamlDirectory;
        _promptStarters = promptStarters;
        _promptEnders = promptEnders;
        _modeConfigurator = modeConfigurator;
        _defaultOutputType = defaultOutputType;
        _enableHandoffReasoning = enableHandoffReasoning;
        _extensibiltyLoader = extensibiltyLoader;
        _gpt5Enabled = gpt5Enabled;
        _agentMemoryRetrievalEnabled = agentMemoryRetrievalEnabled;
        _scheduledTasksEnabled = scheduledTasksEnabled;
    }

    protected override async Task InitializeAsyncCore()
    {
        _logger.LogInternalInformation("Waiting for ToolFactory to initialize...");
        await _toolFactory.InitializeAsync();
        _logger.LogInternalInformation("ToolFactory initialization completed. Registered tool count: {ToolCount}", _toolFactory.RegisteredToolCount);
        await InitializeAgents();
    }

    private void ValidateAgentDescriptor(IAgentDescriptor? agentDescriptor, bool isCustomAgent, bool overwrite = false)
    {
        if (agentDescriptor is null)
        {
            throw new Exception("Agent descriptor is null.");
        }

        if (string.IsNullOrEmpty(agentDescriptor.Name))
        {
            throw new Exception($"Agent descriptor {agentDescriptor.GetType().Name} does not have a name.");
        }

        if (string.IsNullOrEmpty(agentDescriptor.Instructions))
        {
            throw new Exception($"Agent descriptor {agentDescriptor.Name} does not have instructions.");
        }

        if (_agents.ContainsKey(agentDescriptor.Name) && !isCustomAgent && !overwrite)
        {
            throw new Exception($"Agent descriptor {agentDescriptor.Name} already exists.");
        }

        if (!isCustomAgent && agentDescriptor.Tools.Any(tool => !_toolFactory.HasTool(tool)))
        {
            var missingTools = agentDescriptor.Tools.Where(tool => !_toolFactory.HasTool(tool)).ToList();
            throw new Exception($"Agent descriptor {agentDescriptor.Name} has tools that do not exist in the tool factory: {string.Join(", ", missingTools)}");
        }

        if (!string.IsNullOrEmpty(agentDescriptor.LlmModelName)
            && !_chatClientProvider.IsModelSupported(agentDescriptor.LlmModelName))
        {
            throw new Exception($"Agent descriptor {agentDescriptor.Name} refers unsupported model deployment: {agentDescriptor.LlmModelName}." +
                $"Supported LLM Model Names are: {string.Join(", ", _chatClientProvider.GetSupportedModels())}");
        }

        if (agentDescriptor.McpTools != null && agentDescriptor.McpTools.Any(tool => !_toolFactory.HasTool(tool)))
        {
            var missingMcpTools = agentDescriptor.McpTools.Where(tool => !_toolFactory.HasTool(tool)).ToList();
            throw new Exception($"Agent descriptor {agentDescriptor.Name} has MCP tools that do not exist in the tool factory: {string.Join(", ", missingMcpTools)}");
        }
    }

    private Agent<TContext> AddAgentDescriptor(IAgentDescriptor agentDescriptor, bool isCustomAgent, bool overwrite = false)
    {
        try
        {
            ValidateAgentDescriptor(agentDescriptor, isCustomAgent, overwrite);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to validate agent descriptor {descriptorName}.", agentDescriptor.Name);
            throw;
        }

        var agent = new Agent<TContext>(agentDescriptor.Name)
        {
            Instructions = agentDescriptor.Instructions,
            HandoffDescription = agentDescriptor.HandoffDescription,
            MaxReflectionCount = agentDescriptor.MaxReflectionCount,
            CustomReflectionNote = agentDescriptor.CustomReflectionNote,
            Handoffs = [], // Will be populated later to avoid circular references
            CriticOnHandOff = agentDescriptor.CriticOnHandOff,
            FactoryTools = [.. agentDescriptor.Tools, .. agentDescriptor.McpTools],
            // TODO: parallel tool calls not supported in the framework yet, ignore agent-level overrides
            AllowParallelToolCalls = false, // agentDescriptor.AllowParallelToolCalls,
            OutputType = GetOutputType(agentDescriptor),
            UserPromptOverride = agentDescriptor.UserPromptOverride,
            DisableDocumentRetrieval = agentDescriptor.DisableDocumentRetrieval,
            EnableHandoffPromptOverride = agentDescriptor.EnableHandoffPromptOverride,
            DisableCommonPrompts = agentDescriptor.DisableCommonPrompts,

            // === Workflow Agent Properties ===
            AgentType = agentDescriptor.AgentType,
            ParameterExtractionAgent = agentDescriptor.ParameterExtractionAgent,
            OrchestrationStartAgents = agentDescriptor.OrchestrationStartAgents?.ToList() ?? [],
            ResultSummarizationPrompt = agentDescriptor.ResultSummarizationPrompt,
            NextAgentMappings = agentDescriptor.NextAgentMappings?.ToList() ?? []
        };

        if (!agent.FactoryTools.Contains(ToDoWriteTool.ToolName))
        {
            agent.FactoryTools.Add(ToDoWriteTool.ToolName);
        }

        if (!string.IsNullOrEmpty(agentDescriptor.CriticPromptPath))
        {
            agent.CriticPromptPath = Path.Join(AppContext.BaseDirectory, agentDescriptor.CriticPromptPath);
        }

        if (agentDescriptor.Temperature is not null)
        {
            agent.Temperature = agentDescriptor.Temperature.Value;
        }

        if (!string.IsNullOrEmpty(agentDescriptor.LlmModelName))
        {
            agent.ChatClient = _chatClientProvider.GetModelByKey<IChatClient>(agentDescriptor.LlmModelName);
        }

        ConfigureAgentInstructions(agent, agentDescriptor);

        // Add common tools to the agent
        if (agentDescriptor.CommonTools != null && agentDescriptor.CommonTools.Count > 0)
        {
            foreach (var commonToolName in agentDescriptor.CommonTools)
            {
                if (!_commonToolsDescriptors.TryGetValue(commonToolName, out var commonTools))
                {
                    _logger.LogInternalWarning("Agent descriptor {descriptorName} has a common tool {commonToolName} that does not exist.",
                        agentDescriptor.Name, commonToolName);

                    throw new Exception($"Agent descriptor {agentDescriptor.Name} has a common tool {commonToolName} that does not exist.");
                }

                // Add all tools from the common tool definition to the agent's tools list
                agent.FactoryTools.AddRange(commonTools);
            }
        }

        if (_agents.ContainsKey(agentDescriptor.Name) && !isCustomAgent && !overwrite)
        {
            _logger.LogInternalError("Agent with name {agentName} already exists and overwrite is not allowed.", agentDescriptor.Name);
            throw new Exception($"Agent with name {agentDescriptor.Name} already exists and overwrite is not allowed.");
        }

        _agents[agentDescriptor.Name] = agent;
        _agentDescriptors[agentDescriptor.Name] = agentDescriptor;

        // Raise event to notify subscribers that an agent was added or updated
        var changeType = overwrite ? AgentChangeType.Updated : AgentChangeType.Added;
        OnAgentChanged(agentDescriptor.Name, changeType);

        return agent;
    }

    private void ConfigureAgentInstructions(Agent<TContext> agent, IAgentDescriptor agentDescriptor)
    {
        agent.Instructions.WithHandoffInstructions();

        if (_promptStarters is not null)
        {
            foreach (var promptStarter in _promptStarters)
            {
                agent.Instructions.AddPromptStarter(promptStarter);
            }
        }

        // Automatically configure agent for different modes
        _modeConfigurator.ConfigureAgent(agent, agentDescriptor, _promptDescriptors);

        foreach (var commonPromptName in agentDescriptor.CommonPrompts)
        {
            if (!_promptDescriptors.TryGetValue(commonPromptName, out var commonPrompt))
            {
                _logger.LogInternalWarning("Agent descriptor {descriptorName} has a common prompt {commonPromptName} that does not exist.",
                    agentDescriptor.Name, commonPromptName);

                throw new Exception($"Agent descriptor {agentDescriptor.Name} has a common prompt {commonPromptName} that does not exist.");
            }

            agent.Instructions.AddCommonPrompt(commonPrompt.Prompt);
        }

        if (!agentDescriptor.CommonPrompts.Contains("todo_write"))
        {
            agentDescriptor.CommonPrompts.Add("todo_write");
        }

        if (_promptEnders is not null)
        {
            foreach (var promptEnder in _promptEnders)
            {
                agent.Instructions.AddPromptEnder(promptEnder);
            }
        }
    }

    private Type? GetOutputType(IAgentDescriptor agentDescriptor)
    {
        // if null use the default type provided to the factory
        if (agentDescriptor.OutputType is null)
        {
            return _defaultOutputType;
        }

        // check system type 'string'
        if (agentDescriptor.OutputType == "string")
        {
            return typeof(string);
        }

        // check if the type is in the provided assemblies to scan
        var resolvedType = _assembliesToScan.SelectMany(a => a.GetTypes())
            .FirstOrDefault(t => t.Name == agentDescriptor.OutputType)
            ?? throw new InvalidOperationException(
                $"Output type {agentDescriptor.OutputType} not found in assemblies {string.Join(", ", _assembliesToScan.Select(a => a.GetName().Name))}.");

        return resolvedType;
    }

    public void UpdateHandoffs()
    {
        foreach (var agent in _agents.Values)
        {
            var agentDescriptor = _agentDescriptors[agent.Name];
            foreach (var handoff in agentDescriptor.Handoffs)
            {
                if (!_agents.ContainsKey(handoff))
                {
                    var warning = $"Agent descriptor {agentDescriptor.Name} has a handoff to {handoff} but it does not exist.";
                    _logger.LogInternalWarning(warning);
                }
            }
            // Populate handoffs with existing agents only to avoid startup issues
            // Filter out scheduled_task_agent handoffs when scheduled tasks are disabled
            agent.Handoffs = agentDescriptor.Handoffs
              .Where(h => _agents.ContainsKey(h))
              .Where(h => _scheduledTasksEnabled || h != "scheduled_task_agent")
              .Select(h => Handoff<TContext>.Create(
                  agent: _agents[h],
                  enableHandoffReasoning: _enableHandoffReasoning))
              .ToList();
        }
    }

    private void UpdateAgentTools()
    {
        foreach (var agentDescriptor in _agentDescriptors.Values)
        {
            if (agentDescriptor is not YamlAgentDescriptor yamlDescriptor ||
                yamlDescriptor.AgentsAsTools == null ||
                yamlDescriptor.AgentsAsTools.Count == 0)
            {
                continue;
            }

            var agent = _agents[agentDescriptor.Name];

            foreach (var agentAsTool in yamlDescriptor.AgentsAsTools)
            {
                if (!_agents.TryGetValue(agentAsTool.AgentName, out var targetAgent))
                {
                    _logger.LogInternalWarning("Agent {agentName} specified in agents_as_tools for {sourceAgent} does not exist.",
                    agentAsTool.AgentName, agentDescriptor.Name);
                    continue;
                }

                // Initialize agent as tool
                var agentAsToolName = $"use_as_tool_{agentAsTool.AgentName.ToLower().Replace(" ", "_")}";
                var toolDescription = $"Use the {agentAsTool.AgentName} agent to process the input and get a response. Provide your query or instructions as input.";

                var tool = targetAgent.AsTool(
                    name: string.IsNullOrEmpty(agentAsTool.ToolName) ? agentAsToolName : agentAsTool.ToolName,
                    description: string.IsNullOrEmpty(agentAsTool.ToolDescription) ? toolDescription : agentAsTool.ToolDescription,
                    inputDescription: agentAsTool.InputDescription
                );

                agent.Tools.Add(tool);
                agent.AgentsAsTools.Add(tool);

                _logger.LogInternalInformation("Added agent {targetAgent} as tool to agent {sourceAgent}",
                agentAsTool.AgentName, agentDescriptor.Name);
            }
        }
    }

    private async Task InitializeAgents()
    {
        // Order is important here, as tools need to be loaded before agents
        // TODO create a Dependency graph and load in order
        try
        {
            LoadCommonPromptsFromAssembly();
            LoadCommonPromptsFromYaml();
            if (_extensibiltyLoader != null)
            {
                var commonPrompts = await _extensibiltyLoader.LoadExtendedCommonPromptsAsync();
                foreach (var prompt in commonPrompts)
                {
                    _promptDescriptors[prompt.Name] = prompt;
                }
            }
            LoadCommonToolsFromYaml();
            if (_extensibiltyLoader != null)
            {
                var commonTools = await _extensibiltyLoader.LoadExtendedCommonToolsListsAsync();
                foreach (var tool in commonTools)
                {
                    _commonToolsDescriptors[tool.Name] = tool.Tools;
                }
            }

            LoadAgentFromAssembly();
            LoadAgentFromYaml();
            LoadDynamicIncidentManagementAgent();
            if (_gpt5Enabled)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "AgentsGPT5");
                LoadYamlAgentsFromFolder(path, overwriteExistingAgents: true, recursive: true);
            }

            if (_agentMemoryRetrievalEnabled)
            {
                var path = Path.Combine(AppContext.BaseDirectory, "AgentsRag");
                if (_gpt5Enabled)
                {
                    path = Path.Combine(AppContext.BaseDirectory, "AgentsRag", "GPT5");
                }
                LoadYamlAgentsFromFolder(path, overwriteExistingAgents: true, recursive: false);
            }

            if (_extensibiltyLoader != null)
            {
                var extendedAgents = await _extensibiltyLoader.LoadExtendedAgentsAsync();
                foreach (var agent in extendedAgents)
                {
                    try
                    {
                        LoadAgentFromDescriptor(agent, true);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogInternalError(ex, "[AgentFactory:EXT] Failed to load extended agent {AgentName}", agent.Name);
                    }
                }
            }

            UpdateHandoffs();

            UpdateAgentTools();

            // Load experiments but don't apply them - they will be applied per-thread by AgentProvider
            LoadExperimentsFromYaml();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[AgentFactory:INIT_AGENTS] Failure during agent initialization pipeline");
            throw;
        }
    }

    private void AddAgentToMetaAgentHandoffs(string agentName)
    {
        if (!_agentDescriptors.ContainsKey("meta_agent"))
        {
            return;
        }
        var metaAgentDescriptor = _agentDescriptors["meta_agent"];
        if (metaAgentDescriptor != null)
        {
            if (!metaAgentDescriptor.Handoffs.Contains(agentName))
            {
                metaAgentDescriptor.Handoffs.Add(agentName);
            }

            _agentDescriptors["meta_agent"] = metaAgentDescriptor;
        }
    }

    private void LoadAgentFromAssembly()
    {
        var agentDescriptorType = typeof(IAgentDescriptor);
        var aiExcludeType = typeof(AiExcludeAttribute);

        var agentDescriptorTypes = _assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t =>
                agentDescriptorType.IsAssignableFrom(t)
                && !t.IsInterface
                && !t.IsAbstract
                && t.GetCustomAttribute(aiExcludeType, inherit: false) == null
            )
            .ToList();

        if (!agentDescriptorTypes.Any())
        {
            _logger.LogInternalWarning("No agent descriptors found in assembly.");
            return;
        }

        foreach (var agentType in agentDescriptorTypes)
        {
            if (Activator.CreateInstance(agentType) is not IAgentDescriptor agentDescriptor)
            {
                _logger.LogInternalError("Failed to create an instance of {agentType}.", agentType.FullName);
                continue;
            }
            if (agentDescriptor.GetType()?.Name == nameof(YamlAgentDescriptor))
            {
                _logger.LogInternalDebug("Skipping YamlAgentDescriptor type as it's just for parser.");
                continue;
            }

            AddAgentDescriptor(agentDescriptor, false);
            _logger.LogInternalInformation("Successfully loaded agent descriptor '{descriptorName}' from assembly '{assemblyName}'.", agentType.Name, agentType.Assembly.GetName().Name);
        }
    }

    private void LoadAgentFromYaml()
    {
        if (_agentsYamlDirectory is null)
        {
            return;
        }

        if (!Directory.Exists(_agentsYamlDirectory))
        {
            _logger.LogInternalError("Agent YAML directory does not exist: {directory}", _agentsYamlDirectory);
            throw new DirectoryNotFoundException($"Agent YAML directory does not exist: {_agentsYamlDirectory}");
        }

        var yamlFiles = Directory.GetFiles(_agentsYamlDirectory, "*.yaml", SearchOption.AllDirectories)
                       .Concat(Directory.GetFiles(_agentsYamlDirectory, "*.yml", SearchOption.AllDirectories));

        foreach (var yamlFile in yamlFiles)
        {
            try
            {
                var agent = LoadAgentFromYamlContent(File.ReadAllText(yamlFile), false);
                _logger.LogInternalInformation(
                    "Successfully loaded agent descriptor '{agentName}' from YAML file '{yamlFile}'.",
                    agent.Name,
                    yamlFile);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to load agent from YAML file '{yamlFile}'.", yamlFile);
                throw;
            }
        }
    }

    private void LoadDynamicIncidentManagementAgent()
    {
        try
        {
            // Get the DynamicIncidentManagementAgent service from the service provider
            // We access it through the ToolFactory's service provider since AgentFactory doesn't have direct access
            var serviceProvider = GetServiceProvider();

            if (serviceProvider == null)
            {
                _logger.LogInternalWarning("ServiceProvider not accessible. Skipping dynamic agent loading.");
                return;
            }

            // Use reflection to get the service by type name to avoid hard assembly reference
            var serviceTypeName = "Agent.Runtime.Services.DynamicIncidentManagementAgent";
            var serviceType = AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.FullName == serviceTypeName);

            if (serviceType == null)
            {
                _logger.LogInternalWarning("DynamicIncidentManagementAgent type not found. Skipping dynamic agent loading.");
                return;
            }

            var dynamicAgentService = serviceProvider.GetService(serviceType);
            if (dynamicAgentService == null)
            {
                _logger.LogInternalWarning("DynamicIncidentManagementAgent service not registered. Skipping dynamic agent loading.");
                return;
            }

            // Call GetIncidentManagementAgentDescriptor() method via reflection
            var getDescriptorMethod = serviceType.GetMethod("GetIncidentManagementAgentDescriptor");
            if (getDescriptorMethod == null)
            {
                _logger.LogInternalError("GetIncidentManagementAgentDescriptor method not found on DynamicIncidentManagementAgent.");
                return;
            }

            var agentDescriptor = getDescriptorMethod.Invoke(dynamicAgentService, null);
            if (agentDescriptor != null)
            {
                var agent = AddAgentDescriptor((IAgentDescriptor)agentDescriptor, false);
                _logger.LogInternalInformation(
                    "Successfully loaded dynamic incident management agent as '{agentName}'.",
                    agent.Name);
            }
            else
            {
                _logger.LogInternalInformation("No dynamic incident management agent loaded (incident management disabled or not configured).");
            }
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to load dynamic incident management agent.");
        }
    }

    private IServiceProvider? GetServiceProvider()
    {
        // Access service provider through reflection from ToolFactory
        // This is a bit hacky but necessary since AgentFactory doesn't have direct access to service provider
        try
        {
            var toolFactoryType = _toolFactory.GetType();
            var serviceProviderField = toolFactoryType.GetField("_serviceProvider", BindingFlags.NonPublic | BindingFlags.Instance);
            return serviceProviderField?.GetValue(_toolFactory) as IServiceProvider;
        }
        catch
        {
            return null;
        }
    }

    public Agent<TContext> LoadAgentFromYamlContent(string yamlContent, bool isCustomAgent)
    {
        try
        {
            var agentDescriptor = YamlAgentDescriptor.FromYaml(yamlContent);
            var agent = AddAgentDescriptor(agentDescriptor, isCustomAgent);
            if (isCustomAgent)
            {
                AddAgentToMetaAgentHandoffs(agent.Name);
            }
            return agent;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse YAML into AgentDescriptor", ex);
        }
    }

    public void LoadYamlAgentsFromFolder(string folderPath, bool overwriteExistingAgents, bool recursive)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            _logger.LogInternalError("Folder path {folderPath} is invalid or does not exist.", folderPath);
            throw new DirectoryNotFoundException($"Folder path {folderPath} does not exist.");
        }

        var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var yamlFiles = Directory.GetFiles(folderPath, "*.yaml", searchOption)
            .Concat(Directory.GetFiles(folderPath, "*.yml", searchOption));

        foreach (var yamlFile in yamlFiles)
        {
            var agentDescriptor = LoadAgentFromYaml(File.ReadAllText(yamlFile));
            if (agentDescriptor != null)
            {
                AddAgentDescriptor(agentDescriptor, isCustomAgent: false, overwrite: overwriteExistingAgents);
                _logger.LogInternalInformation(
                    "Successfully loaded agent descriptor '{agentName}' from YAML file '{yamlFile}'.",
                    agentDescriptor.Name,
                    yamlFile);
            }
        }
        _logger.LogInformation("Loaded {count} agents from folder {folderPath}.", _agents.Count, folderPath);
    }

    public static YamlAgentDescriptor LoadAgentFromYaml(string yamlContent)
    {
        try
        {
            // Use the updated FromYaml method that handles both structured and flat formats
            return YamlAgentDescriptor.FromYaml(yamlContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse YAML into AgentDescriptor", ex);
        }
    }

    private void LoadCommonPromptsFromAssembly()
    {
        var promptDescriptorInterfaceType = typeof(IPromptDescriptor);
        var promptDescriptorTypes = _assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t => promptDescriptorInterfaceType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        foreach (var promptDescriptorType in promptDescriptorTypes)
        {
            if (Activator.CreateInstance(promptDescriptorType) is not IPromptDescriptor promptDescriptor)
            {
                _logger.LogInternalError("Failed to create an instance of {promptDescriptorType}.", promptDescriptorType.FullName);
                continue;
            }

            if (promptDescriptor.GetType()?.Name == nameof(YamlPromptDescriptor))
            {
                _logger.LogInternalDebug("Skipping YamlPromptDescriptor type as it's just for parser.");
                continue;
            }

            _promptDescriptors[promptDescriptor.Name] = promptDescriptor;
        }
    }

    private void LoadCommonPromptsFromYaml()
    {
        if (_commonPromptsYamlDirectory is null)
        {
            return;
        }

        if (!Directory.Exists(_commonPromptsYamlDirectory))
        {
            _logger.LogInternalError("Common prompts YAML directory does not exist: {directory}", _commonPromptsYamlDirectory);
            throw new DirectoryNotFoundException($"Common prompts YAML directory does not exist: {_commonPromptsYamlDirectory}");
        }

        var yamlFiles = Directory.GetFiles(_commonPromptsYamlDirectory, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_commonPromptsYamlDirectory, "*.yml", SearchOption.AllDirectories));

        foreach (var yamlFile in yamlFiles)
        {
            LoadCommonPromptFromFile(yamlFile);
        }
    }

    private void LoadCommonPromptFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("YAML file not found", filePath);
            }

            string yamlContent = File.ReadAllText(filePath);
            var promptDescriptor = LoadCommonPromptFromYaml(yamlContent);
            _promptDescriptors[promptDescriptor.Name] = promptDescriptor;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to load common prompt from file {filePath}.", filePath);
            throw;
        }
    }

    public void LoadCommonPromptFromDescriptor(YamlPromptDescriptor prompt)
    {
        _promptDescriptors[prompt.Name] = prompt;
    }

    public void LoadCommonToolsListFromDescriptor(YamlCommonToolsDescriptor toolsList)
    {
        _commonToolsDescriptors[toolsList.Name] = toolsList.Tools;
    }

    private static YamlPromptDescriptor LoadCommonPromptFromYaml(string yamlContent)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var promptDescriptor = deserializer.Deserialize<YamlPromptDescriptor>(yamlContent);
            return promptDescriptor;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse YAML into PromptDescriptor", ex);
        }
    }

    private void LoadCommonToolsFromYaml()
    {
        if (_commonToolsYamlDirectory is null)
        {
            return;
        }

        if (!Directory.Exists(_commonToolsYamlDirectory))
        {
            _logger.LogInternalError("Common tools YAML directory does not exist: {directory}", _commonToolsYamlDirectory);
            throw new DirectoryNotFoundException($"Common tools YAML directory does not exist: {_commonToolsYamlDirectory}");
        }

        var yamlFiles = Directory.GetFiles(_commonToolsYamlDirectory, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_commonToolsYamlDirectory, "*.yml", SearchOption.AllDirectories));

        foreach (var yamlFile in yamlFiles)
        {
            LoadCommonToolsFromFile(yamlFile);
        }
    }

    private void LoadCommonToolsFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException("YAML file not found", filePath);
            }

            string yamlContent = File.ReadAllText(filePath);
            var toolsDescriptor = LoadCommonToolsFromYaml(yamlContent);
            _commonToolsDescriptors[toolsDescriptor.Name] = toolsDescriptor.Tools;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to load common tools from file {filePath}.", filePath);
            throw;
        }
    }

    private static YamlCommonToolsDescriptor LoadCommonToolsFromYaml(string yamlContent)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var toolsDescriptor = deserializer.Deserialize<YamlCommonToolsDescriptor>(yamlContent);
            return toolsDescriptor;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse YAML into CommonToolsDescriptor", ex);
        }
    }

    public bool AgentExists(string agentName)
    {
        return _agents.ContainsKey(agentName);
    }

    public Agent<TContext> GetAgent(string name)
    {
        var agentFound = _agents.TryGetValue(name, out var agent);
        if (!agentFound || agent is null)
        {
            _logger.LogInternalError("Agent {agentName} not found.", name);
            throw new KeyNotFoundException($"Agent {name} not found.");
        }

        return agent;
    }

    public List<Agent<TContext>> GetAllAgents()
    {
        return [.. _agents.Values];
    }

    public List<IPromptDescriptor> GetAllCommonPrompts()
    {
        return [.. _promptDescriptors.Values];
    }

    public Dictionary<string, List<string>> GetAllCommonTools()
    {
        return _commonToolsDescriptors.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());
    }

    public List<IAgentDescriptor> GetAllAgentDescriptors()
    {
        return [.. _agentDescriptors.Values];
    }

    public Agent<TContext> LoadAgentFromDescriptor(YamlAgentDescriptor agentDescriptor, bool isCustomAgent)
    {
        try
        {
            var agent = AddAgentDescriptor(agentDescriptor, isCustomAgent);
            if (isCustomAgent && agentDescriptor.Name != "meta_agent")
            {
                AddAgentToMetaAgentHandoffs(agent.Name);
            }
            return agent;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to load agent from descriptor {descriptorName}.", agentDescriptor.Name);
            throw;
        }
    }

    public IReadOnlyDictionary<string, IPromptDescriptor> PromptDescriptors =>
       _promptDescriptors.AsReadOnly();

    private void LoadExperimentsFromYaml()
    {
        if (_experimentsYamlDirectory is null)
        {
            return;
        }

        var yamlFiles = Directory.GetFiles(_experimentsYamlDirectory, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(_experimentsYamlDirectory, "*.yml", SearchOption.AllDirectories));

        foreach (var yamlFile in yamlFiles)
        {
            try
            {
                string yamlContent = File.ReadAllText(yamlFile);
                var experiment = Experiment.FromYaml(yamlContent);
                _experiments.Add(experiment);
                _logger.LogInternalInformation(
                    "Successfully loaded experiment '{experimentId}' from YAML file '{yamlFile}'.",
                    experiment.ExperimentId,
                    yamlFile);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to load experiment from file {filePath}.", yamlFile);
            }
        }
    }

    /// <summary>
    /// Applies a variant overlay to a provided agent graph.
    /// This method is public to allow AgentProvider to apply overlays to cloned agent graphs.
    /// </summary>
    /// <param name="agentGraph">The agent graph to apply the overlay to</param>
    /// <param name="overlay">The variant overlay to apply</param>
    public void ApplyVariantOverlayToGraph(Dictionary<string, Agent<TContext>> agentGraph, VariantOverlay overlay)
    {
        if (overlay.PromptOverlay != null)
        {
            foreach (var po in overlay.PromptOverlay)
            {
                if (OverlayAppliesToAllAgents(po.AgentNames))
                {
                    foreach (var agent in agentGraph.Values)
                    {
                        ApplyPromptOverlay(agent, po);
                    }
                }
                else
                {
                    foreach (var agentName in po.AgentNames)
                    {
                        if (agentGraph.TryGetValue(agentName, out var agent))
                        {
                            ApplyPromptOverlay(agent, po);
                        }
                    }
                }
            }
        }

        if (overlay.ToolOverlay != null)
        {
            foreach (var to in overlay.ToolOverlay)
            {
                if (OverlayAppliesToAllAgents(to.AgentNames))
                {
                    foreach (var agent in agentGraph.Values)
                    {
                        ApplyToolOverlay(agent, to);
                    }
                }
                else
                {
                    foreach (var agentName in to.AgentNames)
                    {
                        if (agentGraph.TryGetValue(agentName, out var agent))
                        {
                            ApplyToolOverlay(agent, to);
                        }
                    }
                }
            }
        }

        if (overlay.HandoffOverlay != null)
        {
            foreach (var ho in overlay.HandoffOverlay)
            {
                if (OverlayAppliesToAllAgents(ho.AgentNames))
                {
                    foreach (var agent in agentGraph.Values)
                    {
                        ApplyHandoffOverlay(agent, ho, agentGraph);
                    }
                }
                else
                {
                    foreach (var agentName in ho.AgentNames)
                    {
                        if (agentGraph.TryGetValue(agentName, out var agent))
                        {
                            ApplyHandoffOverlay(agent, ho, agentGraph);
                        }
                    }
                }
            }
        }

        if (overlay.ParamOverlay != null)
        {
            foreach (var pa in overlay.ParamOverlay)
            {
                if (OverlayAppliesToAllAgents(pa.AgentNames))
                {
                    foreach (var agent in agentGraph.Values)
                    {
                        ApplyParamOverlay(agent, pa);
                    }
                }
                else
                {
                    foreach (var agentName in pa.AgentNames)
                    {
                        if (agentGraph.TryGetValue(agentName, out var agent))
                        {
                            ApplyParamOverlay(agent, pa);
                        }
                    }
                }
            }
        }
    }

    private static bool OverlayAppliesToAllAgents(IEnumerable<string> agentNames)
    {
        return agentNames.Contains("*");
    }

    private void ApplyPromptOverlay(Agent<TContext> agent, PromptOverlay overlay)
    {
        if (overlay.ReplaceSystemPrompt != null)
        {
            // replace base system prompt
            agent.Instructions = overlay.ReplaceSystemPrompt;

            // add handoff instructions if specified
            if (overlay.HasHandoffInstructions)
            {
                agent.Instructions.WithHandoffInstructions();
            }

            // re-apply standard modifiers if specified (this will override custom value for 'HasHandoffInstructions' above)
            if (overlay.ApplyStandardModifiers)
            {
                ConfigureAgentInstructions(agent, _agentDescriptors[agent.Name]);
            }
        }
        if (overlay.PrependSystemPrompt != null)
        {
            agent.Instructions = overlay.PrependSystemPrompt + "\n" + agent.Instructions;
        }
        if (overlay.AppendSystemPrompt != null)
        {
            agent.Instructions += "\n" + overlay.AppendSystemPrompt;
        }
        if (overlay.HandoffInstructions != null)
        {
            agent.HandoffDescription = overlay.HandoffInstructions;
        }

        // add custom common prompts
        if (overlay.CommonPrompts != null)
        {
            foreach (var commonPromptName in overlay.CommonPrompts)
            {
                if (!_promptDescriptors.TryGetValue(commonPromptName, out var commonPrompt))
                {
                    _logger.LogInternalWarning("Prompt overlay has a common prompt {commonPromptName} that does not exist.",
                        commonPromptName);

                    continue;
                }

                if (_agentDescriptors[agent.Name].CommonPrompts.Contains(commonPromptName) && overlay.ApplyStandardModifiers)
                {
                    // already added in base instructions
                    continue;
                }

                agent.Instructions.AddCommonPrompt(commonPrompt.Prompt);
            }
        }
    }

    private static void ApplyToolOverlay(Agent<TContext> agent, ToolOverlay overlay)
    {
        if (overlay.ReplaceTools != null)
        {
            agent.FactoryTools = [.. overlay.ReplaceTools];
        }
        if (overlay.AddTools != null)
        {
            foreach (var tool in overlay.AddTools)
            {
                if (!agent.FactoryTools.Contains(tool))
                {
                    agent.FactoryTools.Add(tool);
                }
            }
        }
        if (overlay.RemoveTools != null)
        {
            foreach (var tool in overlay.RemoveTools)
            {
                agent.FactoryTools.Remove(tool);
            }
        }
    }

    private void ApplyHandoffOverlay(Agent<TContext> agent, HandoffOverlay overlay, Dictionary<string, Agent<TContext>>? agentGraph = null)
    {
        var agents = agentGraph ?? _agents;

        if (overlay.ReplaceHandoffs != null)
        {
            agent.Handoffs = [.. overlay.ReplaceHandoffs
              .Where(agents.ContainsKey)
              .Where(h => _scheduledTasksEnabled || h != "scheduled_task_agent")
              .Select(h => Handoff<TContext>.Create(
                  agent: agents[h],
                  enableHandoffReasoning: _enableHandoffReasoning))];
        }
        if (overlay.AddHandoffs != null)
        {
            foreach (var handoff in overlay.AddHandoffs)
            {
                if (agents.ContainsKey(handoff) && !agent.Handoffs.Any(h => h.AgentName == handoff))
                {
                    if (_scheduledTasksEnabled || handoff != "scheduled_task_agent")
                    {
                        agent.Handoffs.Add(Handoff<TContext>.Create(
                            agent: agents[handoff],
                            enableHandoffReasoning: _enableHandoffReasoning));
                    }
                }
            }
        }
        if (overlay.RemoveHandoffs != null)
        {
            foreach (var handoff in overlay.RemoveHandoffs)
            {
                agent.Handoffs.RemoveAll(h => h.AgentName == handoff);
            }
        }
    }

    private void ApplyParamOverlay(Agent<TContext> agent, ParamOverlay overlay)
    {
        if (overlay.ModelName != null && _chatClientProvider.IsModelSupported(overlay.ModelName))
        {
            agent.ChatClient = _chatClientProvider.GetModelByKey<IChatClient>(overlay.ModelName);
        }
        if (overlay.ReasoningEffortLevel != null)
        {
            agent.ReasoningEffortLevel = overlay.ReasoningEffortLevel;
        }
        if (overlay.OutputType != null)
        {
            // check system type 'string'
            if (overlay.OutputType == "string")
            {
                agent.OutputType = typeof(string);
            }
            else
            {
                var resolvedType = _assembliesToScan.SelectMany(a => a.GetTypes())
                    .FirstOrDefault(t => t.Name == overlay.OutputType)
                    ?? throw new InvalidOperationException(
                        $"Output type {overlay.OutputType} not found in assemblies {string.Join(", ", _assembliesToScan.Select(a => a.GetName().Name))}.");
            }
        }
    }

    public IReadOnlyList<Experiment> Experiments => _experiments.AsReadOnly();

    /// <summary>
    /// Raises the AgentChanged event to notify subscribers that an agent has been modified
    /// </summary>
    /// <param name="agentName">The name of the agent that changed</param>
    /// <param name="changeType">The type of change that occurred</param>
    private void OnAgentChanged(string agentName, AgentChangeType changeType)
    {
        try
        {
            AgentChanged?.Invoke(this, new AgentChangedEventArgs(agentName, changeType));
            _logger.LogInternalDebug("AgentChanged event raised for agent '{AgentName}' with change type '{ChangeType}'", agentName, changeType);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error raising AgentChanged event for agent '{AgentName}'", agentName);
        }
    }
}
