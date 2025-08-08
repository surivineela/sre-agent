// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Framework;

public interface IAgentExistenceChecker
{
    bool AgentExists(string agentName);
}

public interface IAgentFactory<TContext>
    where TContext : class
{
    public Agent<TContext> GetAgent(string name);

    public IReadOnlyDictionary<string, IPromptDescriptor> PromptDescriptors { get; }

    public Agent<TContext> LoadAgentFromYamlContent(string yamlContent, bool isCustomAgent);

    public Agent<TContext> LoadAgentFromDescriptor(YamlAgentDescriptor yamlContent, bool isCustomAgent);

    // Overwrite existing agent agents, useful for loading agents with different prompts when some feature flags are enabled, e.g agent memory RAG
    public void LoadYamlAgentsFromFolder(string folderPath, bool overwriteExistingAgents);

    public void LoadExtendedAgentsFromFolder(string folderPath, bool isCustomAgent);

    void UpdateHandoffs();
}

public class AgentFactory<TContext> : IAgentFactory<TContext>
    where TContext : class
{
    // A map from Agent name -> Agent descriptor
    private readonly Dictionary<string, Agent<TContext>> _agents = [];

    private readonly Dictionary<string, IAgentDescriptor> _agentDescriptors = [];
    private readonly Dictionary<string, IPromptDescriptor> _promptDescriptors = [];
    private readonly Dictionary<string, List<string>> _commonToolsDescriptors = [];
    private readonly ILogger<AgentFactory<TContext>> _logger;
    private readonly IToolFactory<TContext> _toolFactory;
    private readonly IEnumerable<Assembly> _assembliesToScan;
    private readonly string? _agentsYamlDirectory;
    private readonly string? _commonPromptsYamlDirectory;
    private readonly string? _commonToolsYamlDirectory;
    private readonly IEnumerable<string>? _promptStarters;
    private readonly IEnumerable<string>? _promptEnders;
    private readonly Type? _defaultOutputType;
    private readonly IAgentModeConfigurator<TContext> _modeConfigurator;
    private readonly bool _enableHandoffReasoning;

    // Update constructor to include the optional repository parameter
    public AgentFactory(
        ILogger<AgentFactory<TContext>> logger,
        IToolFactory<TContext> toolFactory,
        IEnumerable<Assembly> assembliesToScan,
        IAgentModeConfigurator<TContext> modeConfigurator,
        string? agentsYamlDirectory = null,
        string? commonPromptsYamlDirectory = null,
        string? commonToolsYamlDirectory = null,
        IEnumerable<string>? promptStarters = null,
        IEnumerable<string>? promptEnders = null,
        Type? defaultOutputType = null,
        bool enableHandoffReasoning = false
    )
    {
        _toolFactory = toolFactory;
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
        InitializeAgents();
    }

    public void ValidateAgentDescriptor(IAgentDescriptor? agentDescriptor, bool isCustomAgent, bool overwrite = false)
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
    }

    private Agent<TContext> AddAgentDescriptor(IAgentDescriptor agentDescriptor, bool isCustomAgent, bool overwrite = false)
    {
        try
        {
            ValidateAgentDescriptor(agentDescriptor, isCustomAgent, overwrite);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to validate agent descriptor {descriptorName}.", agentDescriptor.Name);
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
            FactoryTools = agentDescriptor.Tools.Select(tool => tool).ToList<string>(),
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

        if (!string.IsNullOrEmpty(agentDescriptor.CriticPromptPath))
        {
            agent.CriticPromptPath = Path.Join(AppContext.BaseDirectory, agentDescriptor.CriticPromptPath);
        }

        if (agentDescriptor.Temperature is not null)
        {
            agent.Temperature = agentDescriptor.Temperature.Value;
        }

        agent.Instructions
            .WithHandoffInstructions();

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
                _logger.LogWarning("Agent descriptor {descriptorName} has a common prompt {commonPromptName} that does not exist.",
                    agentDescriptor.Name, commonPromptName);

                throw new Exception($"Agent descriptor {agentDescriptor.Name} has a common prompt {commonPromptName} that does not exist.");
            }

            agent.Instructions.AddCommonPrompt(commonPrompt.Prompt);
        }

        // Add common tools to the agent
        if (agentDescriptor.CommonTools != null && agentDescriptor.CommonTools.Count > 0)
        {
            foreach (var commonToolName in agentDescriptor.CommonTools)
            {
                if (!_commonToolsDescriptors.TryGetValue(commonToolName, out var commonTools))
                {
                    _logger.LogWarning("Agent descriptor {descriptorName} has a common tool {commonToolName} that does not exist.",
                        agentDescriptor.Name, commonToolName);

                    throw new Exception($"Agent descriptor {agentDescriptor.Name} has a common tool {commonToolName} that does not exist.");
                }

                // Add all tools from the common tool definition to the agent's tools list
                agent.FactoryTools.AddRange(commonTools);
            }
        }

        if (_promptEnders is not null)
        {
            foreach (var promptEnder in _promptEnders)
            {
                agent.Instructions.AddPromptEnder(promptEnder);
            }
        }

        if (_agents.ContainsKey(agentDescriptor.Name) && !isCustomAgent && !overwrite)
        {
            _logger.LogError("Agent with name {agentName} already exists and overwrite is not allowed.", agentDescriptor.Name);
            throw new Exception($"Agent with name {agentDescriptor.Name} already exists and overwrite is not allowed.");
        }

        _agents[agentDescriptor.Name] = agent;
        _agentDescriptors[agentDescriptor.Name] = agentDescriptor;
        return agent;
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
                    var error = $"Agent descriptor {agentDescriptor.Name} has a handoff to {handoff} but it does not exist.";
                    _logger.LogError(error);
                    throw new KeyNotFoundException(error);
                }
            }

            agent.Handoffs = agentDescriptor.Handoffs
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
                    _logger.LogWarning("Agent {agentName} specified in agents_as_tools for {sourceAgent} does not exist.",
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

                _logger.LogInformation("Added agent {targetAgent} as tool to agent {sourceAgent}",
                agentAsTool.AgentName, agentDescriptor.Name);
            }
        }
    }

    private void InitializeAgents()
    {
        LoadCommonPromptsFromAssembly();
        LoadCommonPromptsFromYaml();
        LoadCommonToolsFromYaml();
        LoadAgentFromAssembly();
        LoadAgentFromYaml();
        UpdateHandoffs();
        UpdateAgentTools();
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
        UpdateHandoffs();
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
            _logger.LogWarning("No agent descriptors found in assembly.");
            return;
        }

        foreach (var agentType in agentDescriptorTypes)
        {
            if (Activator.CreateInstance(agentType) is not IAgentDescriptor agentDescriptor)
            {
                _logger.LogError("Failed to create an instance of {agentType}.", agentType.FullName);
                continue;
            }
            if (agentDescriptor.GetType()?.Name == nameof(YamlAgentDescriptor))
            {
                _logger.LogDebug("Skipping YamlAgentDescriptor type as it's just for parser.");
                continue;
            }

            AddAgentDescriptor(agentDescriptor, false);
            _logger.LogInformation("Successfully loaded agent descriptor '{descriptorName}' from assembly '{assemblyName}'.", agentType.Name, agentType.Assembly.GetName().Name);
        }
    }

    private void LoadAgentFromYaml()
    {
        if (_agentsYamlDirectory is null)
        {
            return;
        }

        var yamlFiles = Directory.GetFiles(_agentsYamlDirectory, "*.yaml", SearchOption.AllDirectories)
                       .Concat(Directory.GetFiles(_agentsYamlDirectory, "*.yml", SearchOption.AllDirectories));

        foreach (var yamlFile in yamlFiles)
        {
            var agent = LoadAgentFromYamlContent(File.ReadAllText(yamlFile), false);
            _logger.LogInformation(
                "Successfully loaded agent descriptor '{agentName}' from YAML file '{yamlFile}'.",
                agent.Name,
                yamlFile);
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

    public void LoadYamlAgentsFromFolder(string folderPath, bool overwriteExistingAgents)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            _logger.LogError("Folder path {folderPath} is invalid or does not exist.", folderPath);
            throw new DirectoryNotFoundException($"Folder path {folderPath} does not exist.");
        }

        var yamlFiles = Directory.GetFiles(folderPath, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(folderPath, "*.yml", SearchOption.AllDirectories));

        foreach (var yamlFile in yamlFiles)
        {
            var agentDescriptor = LoadAgentFromYaml(File.ReadAllText(yamlFile));
            if (agentDescriptor != null)
            {
                AddAgentDescriptor(agentDescriptor, isCustomAgent: false, overwrite: overwriteExistingAgents);
                _logger.LogInformation(
                    "Successfully loaded agent descriptor '{agentName}' from YAML file '{yamlFile}'.",
                    agentDescriptor.Name,
                    yamlFile);
            }
        }
        _logger.LogInformation("Loaded {count} agents from folder {folderPath}.", _agents.Count, folderPath);
        UpdateHandoffs();
    }


    // TODO: Replace to load extended agents from cosmos DB directly
    public void LoadExtendedAgentsFromFolder(string folderPath, bool isCustomAgent)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            _logger.LogError("Folder path {folderPath} is invalid or does not exist.", folderPath);
            throw new DirectoryNotFoundException($"Folder path {folderPath} does not exist.");
        }
        var yamlFiles = Directory.GetFiles(folderPath, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(folderPath, "*.yml", SearchOption.AllDirectories));
        foreach (var yamlFile in yamlFiles)
        {
            // if yaml file path contains "/tools/" ignore them
            if (yamlFile.Contains(@"\tools\", StringComparison.InvariantCultureIgnoreCase))
            {
                continue;
            }
            try
            {
                var agent = LoadAgentFromYamlContent(File.ReadAllText(yamlFile), isCustomAgent);

                _logger.LogInformation(
                    "Successfully loaded extended agent descriptor '{agentName}' from YAML file '{yamlFile}'.",
                    agent.Name,
                    yamlFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load agent from file {yamlFile}.", yamlFile);
                throw;
            }
        }
    }

    public static YamlAgentDescriptor LoadAgentFromYaml(string yamlContent)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var agentDescriptor = deserializer.Deserialize<YamlAgentDescriptor>(yamlContent);
            return agentDescriptor;
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
                _logger.LogError("Failed to create an instance of {promptDescriptorType}.", promptDescriptorType.FullName);
                continue;
            }

            if (promptDescriptor.GetType()?.Name == nameof(YamlPromptDescriptor))
            {
                _logger.LogDebug("Skipping YamlPromptDescriptor type as it's just for parser.");
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
            _logger.LogError(ex, "Failed to load common prompt from file {filePath}.", filePath);
            throw;
        }
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
            _logger.LogError(ex, "Failed to load common tools from file {filePath}.", filePath);
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
            _logger.LogError("Agent {agentName} not found.", name);
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
            if (isCustomAgent)
            {
                AddAgentToMetaAgentHandoffs(agent.Name);
            }
            return agent;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to load agent from descriptor", ex);
        }
    }

    public IReadOnlyDictionary<string, IPromptDescriptor> PromptDescriptors =>
       _promptDescriptors.AsReadOnly();
}
