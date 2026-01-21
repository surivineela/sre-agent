// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Framework.Skills;
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

    public int RegisteredBuiltInAgentCount { get; }

    public int RegisteredExtendedAgentCount { get; }

    public Agent<TContext> LoadAgentFromYamlContent(string yamlContent, bool isCustomAgent);

    public Agent<TContext> LoadAgentFromDescriptor(YamlAgentDescriptor yamlContent, bool isCustomAgent);

    // Overwrite existing agent agents, useful for loading agents with different prompts when some feature flags are enabled, e.g agent memory RAG
    public void LoadYamlAgentsFromFolder(string folderPath, bool overwriteExistingAgents, bool recursive);

    public void LoadCommonPromptFromDescriptor(YamlPromptDescriptor prompt);

    public void LoadCommonToolsListFromDescriptor(YamlCommonToolsDescriptor toolsList);

    void UpdateHandoffs();

    List<IAgentDescriptor> GetAllAgentDescriptors();

    // Attempt loading deferred MCP agent descriptors after MCP tools become available
    void AttemptLoadDeferredMcpAgents();
}

public sealed class AgentFactory<TContext> : AsyncInitializerBase, IAgentFactory<TContext>
    where TContext : class
{
    // A map from Agent name -> Agent descriptor
    private readonly Dictionary<string, Agent<TContext>> _agents = new(StringComparer.OrdinalIgnoreCase);

    private readonly Dictionary<string, IAgentDescriptor> _agentDescriptors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, IPromptDescriptor> _promptDescriptors = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<string>> _commonToolsDescriptors = new(StringComparer.OrdinalIgnoreCase);
    private readonly ILogger<AgentFactory<TContext>> _logger;
    private readonly IToolFactory<TContext> _toolFactory;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly IEnumerable<Assembly> _assembliesToScan;
    private readonly string? _agentsYamlDirectory;
    private readonly string? _commonPromptsYamlDirectory;
    private readonly string? _commonToolsYamlDirectory;
    private readonly IEnumerable<string>? _promptStarters;
    private readonly IEnumerable<string>? _promptEnders;
    private readonly Type? _defaultOutputType;
    private readonly IAgentModeConfigurator<TContext> _modeConfigurator;
    private readonly bool _enableHandoffReasoning;
    private readonly IExtensibilityLoader? _extensibiltyLoader;
    private PromptTemplateResolver? _templateResolver;
    private readonly IEnumerable<Func<IAgentDescriptor?>> _dynamicAgentDescriptors = [];

    /// <summary>
    /// Gets whether handoff reasoning is enabled for agents created by this factory.
    /// Exposed for use by AgentProvider.
    /// </summary>
    public bool EnableHandoffReasoning => _enableHandoffReasoning;
    private readonly bool _gpt5Enabled;
    private readonly bool _agentMemoryRetrievalEnabled;
    private readonly bool _scheduledTasksEnabled;
    private readonly bool _enablePartialOutput;
    private readonly bool _workspaceToolsEnabled;

    // NEW: store deferred MCP agent descriptors (descriptor, isCustom, overwrite)
    private readonly List<(IAgentDescriptor Descriptor, bool IsCustom, bool Overwrite)> _deferredMcpAgentDescriptors = [];

    /// <summary>
    /// Event raised when an agent is added, updated, or removed from the factory
    /// </summary>
    public event EventHandler<AgentChangedEventArgs>? AgentChanged;

    public int RegisteredAgentCount => _agents.Count;

    public int RegisteredBuiltInAgentCount => _agents.Values.Count(a => !a.IsExtended);

    public int RegisteredExtendedAgentCount => _agents.Values.Count(a => a.IsExtended);

    public AgentFactory(
        ILogger<AgentFactory<TContext>> logger,
        IToolFactory<TContext> toolFactory,
        IChatClientProvider chatClientProvider,
        IEnumerable<Assembly> assembliesToScan,
        IAgentModeConfigurator<TContext> modeConfigurator,
        string? agentsYamlDirectory = null,
        string? commonPromptsYamlDirectory = null,
        string? commonToolsYamlDirectory = null,
        IEnumerable<string>? promptStarters = null,
        IEnumerable<string>? promptEnders = null,
        Type? defaultOutputType = null,
        bool enableHandoffReasoning = false,
        IExtensibilityLoader? extensibiltyLoader = null,
        bool gpt5Enabled = false,
        bool agentMemoryRetrievalEnabled = false,
        bool scheduledTasksEnabled = false,
        bool enablePartialOutput = false,
        IEnumerable<Func<IAgentDescriptor?>>? dynamicAgentDescriptors = null,
        bool workspaceToolsEnabled = false
    )
    {
        _toolFactory = toolFactory;
        _chatClientProvider = chatClientProvider;
        _logger = logger;
        _assembliesToScan = assembliesToScan;
        _agentsYamlDirectory = agentsYamlDirectory;
        _commonPromptsYamlDirectory = commonPromptsYamlDirectory;
        _commonToolsYamlDirectory = commonToolsYamlDirectory;
        _promptStarters = promptStarters;
        _promptEnders = promptEnders;
        _modeConfigurator = modeConfigurator;
        _defaultOutputType = defaultOutputType;
        _enableHandoffReasoning = enableHandoffReasoning;
        _extensibiltyLoader = extensibiltyLoader;
        _gpt5Enabled = gpt5Enabled;
        _agentMemoryRetrievalEnabled = agentMemoryRetrievalEnabled;
        _scheduledTasksEnabled = scheduledTasksEnabled;
        _enablePartialOutput = enablePartialOutput;
        _workspaceToolsEnabled = workspaceToolsEnabled;

        if (dynamicAgentDescriptors is not null)
        {
            _dynamicAgentDescriptors = dynamicAgentDescriptors;
        }
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
                $"Supported LLM Model Names are: {string.Join(", ", _chatClientProvider.GetAvailableModels())}");
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
            AllowParallelToolCalls = agentDescriptor.AllowParallelToolCalls,
            OutputType = GetOutputType(agentDescriptor),
            UserPromptOverride = agentDescriptor.UserPromptOverride,
            DisableDocumentRetrieval = agentDescriptor.DisableDocumentRetrieval,
            EnableHandoffPromptOverride = agentDescriptor.EnableHandoffPromptOverride,
            IsExtended = isCustomAgent,
            EnableSkills = agentDescriptor.EnableSkills,
            AddSystemSkills = agentDescriptor.AddSystemSkills,
            EnableVanillaMode = agentDescriptor.EnableVanillaMode,

            // === Workflow Agent Properties ===
            AgentType = agentDescriptor.AgentType,
            ParameterExtractionAgent = agentDescriptor.ParameterExtractionAgent,
            OrchestrationStartAgents = agentDescriptor.OrchestrationStartAgents?.ToList() ?? [],
            ResultSummarizationPrompt = agentDescriptor.ResultSummarizationPrompt,
            NextAgentMappings = agentDescriptor.NextAgentMappings?.ToList() ?? []
        };

        // Only add ToDoWrite tool if workspace tools are not enabled
        // (workspace tools provide their own ManageTodoList tool)
        if (!_workspaceToolsEnabled)
        {
            AugmentToDoWrite(agentDescriptor, agent);
        }

        // Automatically add read_skill_file tool if skills are enabled
        AugmentSkills(agentDescriptor, agent);

        // Add memory tools and common prompts
        AugmentMemoryTools(agentDescriptor, agent);

        //TODO: This func could be replaced when experiment is working on extended agents. When experiment is working, use ApplyParamOverlay to set enablePartialOutput and common prompt in agent
        // Add ToolOutputRetriever tool if enabled
        AugmentPartialOutputTool(agent);

        // Add ViewImage tool for image viewing capability
        AugmentViewImageTool(agent);

        // Add common tools to the agent
        if (agentDescriptor.CommonTools is not null
            && agentDescriptor.CommonTools.Count > 0)
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

        if (!string.IsNullOrEmpty(agentDescriptor.CriticPromptPath))
        {
            agent.CriticPromptPath = Path.Join(AppContext.BaseDirectory, agentDescriptor.CriticPromptPath);
        }

        if (agentDescriptor.Temperature is not null)
        {
            agent.Temperature = agentDescriptor.Temperature.Value;
        }

        if (agentDescriptor.LlmScenarioType is not null)
        {
            agent.ChatClient = _chatClientProvider.GetBestModelByScenario(agentDescriptor.LlmScenarioType.Value);
        }
        else if (!string.IsNullOrEmpty(agentDescriptor.LlmModelName))
        {
            agent.ChatClient = _chatClientProvider.GetModelByKey<IChatClient>(agentDescriptor.LlmModelName);
        }

        ConfigureAgentInstructions(agent, agentDescriptor);

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

    private void AugmentMemoryTools(IAgentDescriptor agentDescriptor, Agent<TContext> agent)
    {
        const string SearchMemoryTool = "SearchMemory";
        const string SearchMemoryCommonPrompt = "search_memory";
        const string SearchIncidentsTool = "SearchIncidentKnowledge";
        const string SearchIncidentsCommonPrompt = "search_incidents";

        // Add SearchMemory and SearchIncidents automatically to meta if enabled
        if (_agentMemoryRetrievalEnabled
            && (string.Equals(agent.Name, "meta_agent", StringComparison.OrdinalIgnoreCase)
            || string.Equals(agent.Name, "rca_meta_agent", StringComparison.OrdinalIgnoreCase)))
        {
            // Check if SearchMemory is not already in the tools list
            if (!agent.FactoryTools.Contains(SearchMemoryTool))
            {
                agent.FactoryTools.Add(SearchMemoryTool);
            }
            if (!agent.FactoryTools.Contains(SearchIncidentsTool))
            {
                agent.FactoryTools.Add(SearchIncidentsTool);
            }
        }

        // Add memory common prompts to agents if tools are added
        // Skip if already in CommonPrompts list or if already templated in instructions
        if (agent.FactoryTools.Contains(SearchMemoryTool)
            && !AgentInstructionsContainsCommonPrompt(agentDescriptor, SearchMemoryCommonPrompt))
        {
            agentDescriptor.CommonPrompts.Add(SearchMemoryCommonPrompt);
        }
        if (agent.FactoryTools.Contains(SearchIncidentsTool)
            && !AgentInstructionsContainsCommonPrompt(agentDescriptor, SearchIncidentsCommonPrompt))
        {
            agentDescriptor.CommonPrompts.Add(SearchIncidentsCommonPrompt);
        }
    }

    private static void AugmentToDoWrite(IAgentDescriptor agentDescriptor, Agent<TContext> agent)
    {
        // add todo tool all agents
        if (!agent.FactoryTools.Contains(ToDoWriteTool<TContext>.ToolName))
        {
            agent.FactoryTools.Add(ToDoWriteTool<TContext>.ToolName);
        }

        // add todo common prompt to agent if todo tool is added
        // Skip if already in CommonPrompts list or if already templated in instructions
        if (agent.FactoryTools.Contains(ToDoWriteTool<TContext>.ToolName)
            && !AgentInstructionsContainsCommonPrompt(agentDescriptor, ToDoWriteTool<TContext>.CommonPromptName))
        {
            agentDescriptor.CommonPrompts.Add(ToDoWriteTool<TContext>.CommonPromptName);
        }
    }

    private static void AugmentSkills(IAgentDescriptor agentDescriptor, Agent<TContext> agent)
    {
        if (!agent.EnableSkills)
        {
            return;
        }

        if (!agent.FactoryTools.Contains(ReadSkillFileTool<TContext>.ToolName))
        {
            agent.FactoryTools.Add(ReadSkillFileTool<TContext>.ToolName);
        }

        // add skill common prompt to agent if skill tool is added

        if (agent.FactoryTools.Contains(ReadSkillFileTool<TContext>.ToolName)
            && !AgentInstructionsContainsCommonPrompt(agentDescriptor, ReadSkillFileTool<TContext>.CommonPromptName))
        {
            agentDescriptor.CommonPrompts.Add(ReadSkillFileTool<TContext>.CommonPromptName);
        }
    }

    /// <summary>
    /// Checks if the agent instructions already reference a common prompt,
    /// either directly in the CommonPrompts list or as an inline template.
    /// </summary>
    /// <param name="agentDescriptor">The agent descriptor containing the instructions and common prompts.</param>
    /// <param name="promptName">The name of the common prompt to check for.</param>
    /// <returns>True if the common prompt is referenced, false otherwise.</returns>
    private static bool AgentInstructionsContainsCommonPrompt(IAgentDescriptor agentDescriptor, string promptName)
    {
        return agentDescriptor.CommonPrompts.Contains(promptName)
            || IsPromptTemplated(agentDescriptor.Instructions, promptName);
    }

    /// <summary>
    /// Checks if a common prompt is already referenced as an inline template in the instructions.
    /// Templates use the {{prompt_name}} syntax and are matched case-insensitively.
    /// </summary>
    /// <param name="instructions">The agent instructions to check.</param>
    /// <param name="promptName">The name of the common prompt to look for.</param>
    /// <returns>True if the prompt is templated in the instructions, false otherwise.</returns>
    private static bool IsPromptTemplated(string? instructions, string promptName)
    {
        if (string.IsNullOrEmpty(instructions))
        {
            return false;
        }

        // Check for {{prompt_name}} template syntax (case-insensitive)
        var templatePattern = $"{{{{{promptName}}}}}";
        return instructions.Contains(templatePattern, StringComparison.OrdinalIgnoreCase);
    }

    private void AugmentPartialOutputTool(Agent<TContext> agent)
    {
        const string ToolOutputRetrieverTool = "ToolOutputRetriever";

        // Add ToolOutputRetriever tool if partial output is enabled
        if (_enablePartialOutput)
        {
            // Check if ToolOutputRetriever is not already in the tools list
            if (!agent.FactoryTools.Contains(ToolOutputRetrieverTool))
            {
                agent.FactoryTools.Add(ToolOutputRetrieverTool);
            }
        }
    }

    private static void AugmentViewImageTool(Agent<TContext> agent)
    {
        const string ViewImageTool = "ViewImage";

        // Add ViewImage tool to all agents for image viewing capability
        if (!agent.FactoryTools.Contains(ViewImageTool))
        {
            agent.FactoryTools.Add(ViewImageTool);
        }
    }

    private void ConfigureAgentInstructions(Agent<TContext> agent, IAgentDescriptor agentDescriptor)
    {
        var originalInstructions = agent.Instructions.GetOriginalText();

        // Resolve inline template placeholders {{template_name}} in the system prompt
        var resolvedInstructions = _templateResolver?.ResolveTemplates(
            originalInstructions,
            contextDescription: "agent instructions",
            agentName: agent.Name)
            ?? originalInstructions;

        // Update the Instructions with the resolved text
        agent.Instructions = new PromptText(resolvedInstructions);

        foreach (var commonPromptName in agentDescriptor.CommonPrompts)
        {
            if (!_promptDescriptors.TryGetValue(commonPromptName, out var commonPrompt))
            {
                _logger.LogInternalWarning("Agent descriptor {descriptorName} has a common prompt {commonPromptName} that does not exist.",
                    agentDescriptor.Name, commonPromptName);

                throw new Exception($"Agent descriptor {agentDescriptor.Name} has a common prompt {commonPromptName} that does not exist.");
            }

            agent.Instructions.AddCommonPrompt(commonPrompt.Name, commonPrompt.Prompt);
        }

        // skip the handoff instructions and preamble for vanilla agent
        if (!agent.EnableVanillaMode)
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

            if (_promptEnders is not null)
            {
                foreach (var promptEnder in _promptEnders)
                {
                    agent.Instructions.AddPromptEnder(promptEnder);
                }
            }
        }

        // for system meta agent, always add the mode-specific common prompt (regardless of vanilla mode)
        if (!agent.IsExtended && agent.Name == "meta_agent")
        {
            _modeConfigurator.ConfigureAgent(agent, agentDescriptor, _promptDescriptors);
        }
    }

    private Type? GetOutputType(IAgentDescriptor agentDescriptor)
    {
        // if null use the default type provided to the factory
        if (agentDescriptor.OutputType is null)
        {
            // use string default type in vanilla mode
            return agentDescriptor.EnableVanillaMode
                ? typeof(string)
                : _defaultOutputType;
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
        Agent<TContext>? metaAgent = null;

        List<Agent<TContext>> extendedAgents = [];

        foreach (var agent in _agents.Values)
        {
            if (agent.IsExtended)
            {
                extendedAgents.Add(agent);
            }

            if (agent.Name == "meta_agent")
            {
                metaAgent = agent;
            }

            var agentDescriptor = _agentDescriptors[agent.Name];

            // If agentDescriptor.Handoffs is null, set agent.Handoffs to empty list
            if (agentDescriptor.Handoffs == null)
            {
                agent.Handoffs = [];
                continue;
            }

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
            agent.Handoffs = [.. agentDescriptor.Handoffs
                .Where(_agents.ContainsKey)
                .Where(h => _scheduledTasksEnabled || h != "scheduled_task_agent")
                .Select(h => Handoff<TContext>.Create(
                    agent: _agents[h],
                    enableHandoffReasoning: _enableHandoffReasoning))];
        }

        if (metaAgent != null)
        {
            // Ensure all extended agents are included in meta agent handoffs
            foreach (var extendedAgent in extendedAgents)
            {
                // A user can overwrite meta_agent using extended agents. So skip adding meta_agent to its own handoffs.
                // Also skip agents with null or empty HandoffDescription - they are not intended to be called via handoffs.
                if (!metaAgent.Handoffs.Any(h => h.AgentName == extendedAgent.Name)
                    && extendedAgent.Name != "meta_agent"
                    && !string.IsNullOrEmpty(extendedAgent.HandoffDescription?.GetOriginalText()))
                {
                    metaAgent.Handoffs.Add(Handoff<TContext>.Create(
                        agent: extendedAgent,
                        enableHandoffReasoning: _enableHandoffReasoning));

                    _logger.LogInternalInformation("Added extended agent {extendedAgent} to meta agent handoffs.",
                        extendedAgent.Name);
                }
            }
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

            // Initialize template resolver after all common prompts are loaded
            _templateResolver = new PromptTemplateResolver(_promptDescriptors, _logger);

            LoadCommonToolsFromYaml();
            if (_extensibiltyLoader != null)
            {
                var commonTools = await _extensibiltyLoader.LoadExtendedCommonToolsListsAsync();
                foreach (var tool in commonTools)
                {
                    _commonToolsDescriptors[tool.Name] = tool.Tools;
                }
            }

            LoadAgentsFromAssembly();
            LoadAgentsFromYaml();
            LoadDynamicAgentDescriptors();

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
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "[AgentFactory:INIT_AGENTS] Failure during agent initialization pipeline");
            throw;
        }
    }

    private bool ShouldDeferMcpAgent(IAgentDescriptor descriptor)
        => descriptor.McpTools != null && descriptor.McpTools.Any(t => !_toolFactory.HasTool(t));

    public void AttemptLoadDeferredMcpAgents()
    {
        if (_deferredMcpAgentDescriptors.Count == 0)
        {
            return;
        }

        _logger.LogInternalInformation("Attempting to load {count} deferred MCP agent descriptors.", _deferredMcpAgentDescriptors.Count);

        var loadedMcpAgentDescriptors = new List<(IAgentDescriptor Descriptor, bool IsCustom, bool Overwrite)>();

        foreach (var (descriptor, isCustom, overwrite) in _deferredMcpAgentDescriptors)
        {
            var allMcpToolsReady = descriptor.McpTools != null && descriptor.McpTools.All(t => _toolFactory.HasTool(t));
            if (allMcpToolsReady)
            {
                try
                {
                    AddAgentDescriptor(descriptor, isCustom, overwrite);
                    loadedMcpAgentDescriptors.Add((descriptor, isCustom, overwrite));
                    _logger.LogInternalInformation("Loaded deferred MCP agent '{name}'.", descriptor.Name);
                }
                catch (Exception ex)
                {
                    _logger.LogInternalError(ex, "Failed to load deferred MCP agent '{name}'.", descriptor.Name);
                }
            }
            else
            {
                var missingTools = descriptor.McpTools?.Where(t => !_toolFactory.HasTool(t)).ToArray() ?? Array.Empty<string>();
                _logger.LogInternalDebug("Deferred MCP agent '{name}' still missing tools: {missingTools}", descriptor.Name, string.Join(", ", missingTools));
            }
        }

        _deferredMcpAgentDescriptors.RemoveAll(d => loadedMcpAgentDescriptors.Any(l => l.Descriptor == d.Descriptor));

        if (loadedMcpAgentDescriptors.Count > 0)
        {
            UpdateHandoffs();
            UpdateAgentTools();
        }

        _logger.LogInternalInformation("Deferred MCP agent loading complete. Loaded {loaded}. Remaining {remaining}.", loadedMcpAgentDescriptors.Count, _deferredMcpAgentDescriptors.Count);
    }

    private void LoadDynamicAgentDescriptors()
    {
        foreach (var getAgentDescriptor in _dynamicAgentDescriptors)
        {
            try
            {
                var agentDescriptor = getAgentDescriptor();
                if (agentDescriptor is null)
                {
                    _logger.LogInternalInformation("Dynamic agent descriptor function returned null, skipping.");
                    continue;
                }

                if (ShouldDeferMcpAgent(agentDescriptor))
                {
                    _deferredMcpAgentDescriptors.Add((agentDescriptor, false, false));
                    _logger.LogInternalInformation("Deferring dynamic MCP agent descriptor '{name}'.", agentDescriptor.Name);
                    continue;
                }

                AddAgentDescriptor(agentDescriptor, false);
                _logger.LogInternalInformation("Successfully loaded dynamic agent descriptor '{name}'.", agentDescriptor.Name);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, "Failed to load dynamic agent descriptor.");
            }
        }
    }

    private void LoadAgentsFromAssembly()
    {
        var agentDescriptorType = typeof(IAgentDescriptor);
        var aiExcludeType = typeof(AiExcludeAttribute);
        var agentDescriptorTypes = _assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t => agentDescriptorType.IsAssignableFrom(t)
                        && !t.IsInterface
                        && !t.IsAbstract
                        && t.GetCustomAttribute(aiExcludeType, inherit: false) == null)
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
                _logger.LogInternalError("Failed to create instance of {agentType}.", agentType.FullName);
                continue;
            }

            if (agentDescriptor.GetType().Name == nameof(YamlAgentDescriptor))
            {
                _logger.LogInternalError("Failed to create an instance of {agentType}.", agentType.FullName);
                continue;
            }

            if (ShouldDeferMcpAgent(agentDescriptor))
            {
                _deferredMcpAgentDescriptors.Add((agentDescriptor, false, false));
                _logger.LogInternalInformation("Deferring MCP agent descriptor '{name}'.", agentDescriptor.Name);
                continue;
            }

            AddAgentDescriptor(agentDescriptor, false);
            _logger.LogInternalInformation("Successfully loaded agent descriptor '{descriptorName}' from assembly '{assemblyName}'.", agentType.Name, agentType.Assembly.GetName().Name);
        }
    }

    private void LoadAgentsFromYaml()
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
                var descriptor = LoadAgentDescriptorFromFile(yamlFile);
                if (ShouldDeferMcpAgent(descriptor))
                {
                    _deferredMcpAgentDescriptors.Add((descriptor, false, false));
                    _logger.LogInternalInformation("Deferring MCP agent descriptor '{name}' from file '{file}'.", descriptor.Name, yamlFile);
                    continue;
                }

                var agent = AddAgentDescriptor(descriptor, false);
                _logger.LogInternalInformation("Loaded agent descriptor '{name}' from YAML '{file}'.", agent.Name, yamlFile);
            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, "Failed to load agent from YAML '{file}'.", yamlFile);
                throw;
            }
        }
    }

    public void LoadYamlAgentsFromFolder(string folderPath, bool overwriteExistingAgents, bool recursive)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            _logger.LogInternalError("Folder path {folderPath} is invalid or does not exist.", folderPath);
            throw new DirectoryNotFoundException($"Folder path {folderPath} does not exist.");
        }

        var option = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
        var yamlFiles = Directory.GetFiles(folderPath, "*.yaml", option)
            .Concat(Directory.GetFiles(folderPath, "*.yml", option));

        foreach (var yamlFile in yamlFiles)
        {
            var descriptor = LoadAgentDescriptorFromFile(yamlFile);

            if (ShouldDeferMcpAgent(descriptor))
            {
                _deferredMcpAgentDescriptors.Add((descriptor, false, overwriteExistingAgents));
                _logger.LogInternalInformation("Deferring MCP agent descriptor '{name}' from folder file '{file}'.", descriptor.Name, yamlFile);
                continue;
            }

            AddAgentDescriptor(descriptor, false, overwriteExistingAgents);
            _logger.LogInternalInformation("Loaded agent descriptor '{name}' from folder YAML '{file}'.", descriptor.Name, yamlFile);
        }

        _logger.LogInternalInformation("Loaded {count} agents from folder {folderPath}.", _agents.Count, folderPath);
    }

    private static YamlAgentDescriptor LoadAgentDescriptorFromFile(string filePath)
    {
        try
        {
            var yamlContent = File.ReadAllText(filePath);
            return YamlAgentDescriptor.FromYaml(yamlContent);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse YAML into AgentDescriptor", ex);
        }
    }

    public Agent<TContext> LoadAgentFromYamlContent(string yamlContent, bool isCustomAgent)
    {
        var descriptor = YamlAgentDescriptor.FromYaml(yamlContent);

        if (ShouldDeferMcpAgent(descriptor))
        {
            _deferredMcpAgentDescriptors.Add((descriptor, isCustomAgent, false));
            _logger.LogInternalInformation("Deferring MCP agent descriptor '{name}' loaded from content.", descriptor.Name);
            throw new InvalidOperationException($"Agent '{descriptor.Name}' deferred until MCP tools load.");
        }

        return AddAgentDescriptor(descriptor, isCustomAgent);
    }

    public Agent<TContext> LoadAgentFromDescriptor(YamlAgentDescriptor descriptor, bool isCustomAgent)
    {
        if (ShouldDeferMcpAgent(descriptor))
        {
            _deferredMcpAgentDescriptors.Add((descriptor, isCustomAgent, false));
            _logger.LogInternalInformation("Deferring MCP agent descriptor '{name}'.", descriptor.Name);
            throw new InvalidOperationException($"Agent '{descriptor.Name}' deferred until MCP tools load.");
        }

        return AddAgentDescriptor(descriptor, isCustomAgent);
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
        var yamlContent = File.ReadAllText(filePath);
        var promptDescriptor = LoadCommonPromptFromYaml(yamlContent);
        _promptDescriptors[promptDescriptor.Name] = promptDescriptor;
    }

    public void LoadCommonPromptFromDescriptor(YamlPromptDescriptor prompt) => _promptDescriptors[prompt.Name] = prompt;

    public void LoadCommonToolsListFromDescriptor(YamlCommonToolsDescriptor toolsList) => _commonToolsDescriptors[toolsList.Name] = toolsList.Tools;

    private static YamlPromptDescriptor LoadCommonPromptFromYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
        return deserializer.Deserialize<YamlPromptDescriptor>(yaml);
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
        var yamlContent = File.ReadAllText(filePath);
        var toolsDescriptor = LoadCommonToolsFromYaml(yamlContent);
        _commonToolsDescriptors[toolsDescriptor.Name] = toolsDescriptor.Tools;
    }

    private static YamlCommonToolsDescriptor LoadCommonToolsFromYaml(string yaml)
    {
        var deserializer = new DeserializerBuilder().WithNamingConvention(UnderscoredNamingConvention.Instance).Build();
        return deserializer.Deserialize<YamlCommonToolsDescriptor>(yaml);
    }

    public bool AgentExists(string agentName) => _agents.ContainsKey(agentName);

    public Agent<TContext> GetAgent(string name) => _agents.TryGetValue(name, out var a) ? a : throw new KeyNotFoundException($"Agent {name} not found.");

    public List<Agent<TContext>> GetAllAgents() => [.. _agents.Values];

    public List<IPromptDescriptor> GetAllCommonPrompts() => [.. _promptDescriptors.Values];

    public Dictionary<string, List<string>> GetAllCommonTools() => _commonToolsDescriptors.ToDictionary(kv => kv.Key, kv => kv.Value.ToList());

    public List<IAgentDescriptor> GetAllAgentDescriptors() => [.. _agentDescriptors.Values];

    public IReadOnlyDictionary<string, IPromptDescriptor> PromptDescriptors => _promptDescriptors.AsReadOnly();

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
                ApplyExperimentAspect(agentGraph, po, po.AgentNames, ApplyPromptOverlay);
            }
        }

        if (overlay.ToolOverlay != null)
        {
            foreach (var to in overlay.ToolOverlay)
            {
                ApplyExperimentAspect(agentGraph, to, to.AgentNames, ApplyToolOverlay);
            }
        }

        if (overlay.HandoffOverlay != null)
        {
            foreach (var ho in overlay.HandoffOverlay)
            {
                ApplyExperimentAspect(agentGraph, ho, ho.AgentNames, ApplyHandoffOverlay);
            }
        }

        if (overlay.ParamOverlay != null)
        {
            foreach (var pa in overlay.ParamOverlay)
            {
                ApplyExperimentAspect(agentGraph, pa, pa.AgentNames, ApplyParamOverlay);
            }
        }
    }

    private void ApplyExperimentAspect<T>(
       Dictionary<string, Agent<TContext>> agentGraph,
       T aspect,
       IEnumerable<string> applicableAgents,
       Action<Agent<TContext>, T> applicator)
    {
        if (OverlayAppliesToAllAgents(applicableAgents))
        {
            foreach (var agent in agentGraph.Values)
            {
                applicator(agent, aspect);
            }
        }
        else
        {
            foreach (var agentName in applicableAgents)
            {
                if (agentGraph.TryGetValue(agentName, out var agent))
                {
                    applicator(agent, aspect);
                }
            }
        }
    }

    private void ApplyExperimentAspect<T>(
        Dictionary<string, Agent<TContext>> agentGraph,
        T aspect,
        IEnumerable<string> applicableAgents,
        Action<Agent<TContext>, T, Dictionary<string, Agent<TContext>>?> applicator)
    {
        if (OverlayAppliesToAllAgents(applicableAgents))
        {
            foreach (var agent in agentGraph.Values)
            {
                applicator(agent, aspect, agentGraph);
            }
        }
        else
        {
            foreach (var agentName in applicableAgents)
            {
                if (agentGraph.TryGetValue(agentName, out var agent))
                {
                    applicator(agent, aspect, agentGraph);
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
        if (agent.IsExtended)
        {
            // do not apply prompt overlays to extended agents
            _logger.LogInternalInformation("Skipping prompt overlay for extended agent {agentName}.", agent.Name);

            return;
        }

        _logger.LogInternalInformation("Applying prompt overlay to agent {agentName}.", agent.Name);

        if (overlay.ReplaceSystemPrompt != null)
        {
            // Resolve inline templates in the replacement prompt
            var resolvedPrompt = _templateResolver?.ResolveTemplates(
                overlay.ReplaceSystemPrompt,
                contextDescription: "replacement prompt",
                agentName: agent.Name) ?? overlay.ReplaceSystemPrompt;

            // replace base system prompt
            agent.Instructions = resolvedPrompt;

            // add handoff instructions if specified
            if (overlay.HasHandoffInstructions)
            {
                agent.Instructions.WithHandoffInstructions();
            }

            if (overlay.EnableVanillaMode)
            {
                agent.EnableVanillaMode = true;
            }

            // re-apply standard modifiers if specified (this will override custom value for 'HasHandoffInstructions' above)
            if (overlay.ApplyStandardModifiers)
            {
                ConfigureAgentInstructions(agent, _agentDescriptors[agent.Name]);
            }
        }
        if (overlay.PrependSystemPrompt != null)
        {
            // Resolve inline templates in prepended text
            var resolvedPrepend = _templateResolver?.ResolveTemplates(
                overlay.PrependSystemPrompt,
                contextDescription: "prepend prompt",
                agentName: agent.Name) ?? overlay.PrependSystemPrompt;

            agent.Instructions = resolvedPrepend + "\n" + agent.Instructions;
        }
        if (overlay.AppendSystemPrompt != null)
        {
            // Resolve inline templates in appended text
            var resolvedAppend = _templateResolver?.ResolveTemplates(
                overlay.AppendSystemPrompt,
                contextDescription: "append prompt",
                agentName: agent.Name) ?? overlay.AppendSystemPrompt;

            agent.Instructions += "\n" + resolvedAppend;
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

                if (_agentDescriptors[agent.Name].CommonPrompts.Contains(commonPromptName)
                    && overlay.ApplyStandardModifiers)
                {
                    // already added in base instructions
                    continue;
                }

                agent.Instructions.AddCommonPrompt(commonPrompt.Name, commonPrompt.Prompt);
            }
        }

        if (overlay.UserPromptOverride != null)
        {
            agent.UserPromptOverride = overlay.UserPromptOverride;
        }
    }

    private void ApplyToolOverlay(Agent<TContext> agent, ToolOverlay overlay)
    {
        if (agent.IsExtended)
        {
            // do not apply tool overlays to extended agents
            _logger.LogInternalInformation("Skipping tool overlay for extended agent {agentName}.", agent.Name);

            return;
        }

        _logger.LogInternalInformation("Applying tool overlay to agent {agentName}.", agent.Name);

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
                agent.Tools.RemoveAll(t => t.Name == tool);
            }
        }
    }

    private void ApplyHandoffOverlay(Agent<TContext> agent, HandoffOverlay overlay, Dictionary<string, Agent<TContext>>? agentGraph = null)
    {
        if (agent.IsExtended)
        {
            // do not apply handoff overlays to extended agents
            _logger.LogInternalInformation("Skipping handoff overlay for extended agent {agentName}.", agent.Name);

            return;
        }

        _logger.LogInternalInformation("Applying handoff overlay to agent {agentName}.", agent.Name);

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
        if (agent.IsExtended)
        {
            // do not apply param overlays to extended agents
            _logger.LogInternalInformation("Skipping param overlay for extended agent {agentName}.", agent.Name);

            return;
        }

        _logger.LogInternalInformation("Applying param overlay to agent {agentName}.", agent.Name);

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
        if (overlay.EnableSkills.HasValue)
        {
            agent.EnableSkills = overlay.EnableSkills.Value;
        }
        if (overlay.AddSystemSkills.HasValue)
        {
            agent.AddSystemSkills = overlay.AddSystemSkills.Value;
        }
        if (overlay.AllowParallelToolCalls.HasValue)
        {
            agent.AllowParallelToolCalls = overlay.AllowParallelToolCalls.Value;
        }
    }

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
