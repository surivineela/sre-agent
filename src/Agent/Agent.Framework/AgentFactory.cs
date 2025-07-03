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
    public void LoadAgentsFromFolder(string folderPath);
}

public class AgentFactory<TContext> : IAgentFactory<TContext>
    where TContext : class
{
    // A map from Agent name -> Agent descriptor
    private readonly Dictionary<string, Agent<TContext>> _agents = [];
    private readonly Dictionary<string, IAgentDescriptor> _agentDescriptors = [];
    private readonly Dictionary<string, IPromptDescriptor> _promptDescriptors = [];
    private readonly ILogger<AgentFactory<TContext>> _logger;
    private readonly IToolFactory<TContext> _toolFactory;
    private readonly IEnumerable<Assembly> _assembliesToScan;
    private readonly string? _agentsYamlDirectory;
    private readonly string? _commonPromptsYamlDirectory;
    private readonly IEnumerable<string>? _promptStarters;
    private readonly IEnumerable<string>? _promptEnders;
    private readonly Type? _defaultOutputType;
    private readonly IAgentModeConfigurator<TContext> _modeConfigurator;

    public AgentFactory(
        ILogger<AgentFactory<TContext>> logger,
        IToolFactory<TContext> toolFactory,
        IEnumerable<Assembly> assembliesToScan,
        IAgentModeConfigurator<TContext> modeConfigurator,
        string? agentsYamlDirectory = null,
        string? commonPromptsYamlDirectory = null,
        IEnumerable<string>? promptStarters = null,
        IEnumerable<string>? promptEnders = null,
        Type? defaultOutputType = null
    )
    {
        _toolFactory = toolFactory;
        _logger = logger;
        _assembliesToScan = assembliesToScan;
        _agentsYamlDirectory = agentsYamlDirectory;
        _commonPromptsYamlDirectory = commonPromptsYamlDirectory;
        _promptStarters = promptStarters;
        _promptEnders = promptEnders;
        _modeConfigurator = modeConfigurator;
        _defaultOutputType = defaultOutputType;
        InitializeAgents();
    }

    private void ValidateAgentDescriptor(IAgentDescriptor? agentDescriptor)
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

        if (_agents.ContainsKey(agentDescriptor.Name))
        {
            throw new Exception($"Agent descriptor {agentDescriptor.Name} already exists.");
        }

        if (agentDescriptor.Tools.Any(toolName => !_toolFactory.HasTool(toolName)))
        {
            var missingTools = agentDescriptor.Tools.Where(toolName => !_toolFactory.HasTool(toolName)).ToList();
            throw new Exception($"Agent descriptor {agentDescriptor.Name} has tools that do not exist in the tool factory: {string.Join(", ", missingTools)}");
        }
    }

    private Agent<TContext> AddAgentDescriptor(IAgentDescriptor agentDescriptor)
    {
        try
        {
            ValidateAgentDescriptor(agentDescriptor);
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
            FactoryTools = agentDescriptor.Tools,
            // TODO: parallel tool calls not supported in the framework yet, ignore agent-level overrides
            AllowParallelToolCalls = false, // agentDescriptor.AllowParallelToolCalls,
            OutputType = GetOutputType(agentDescriptor)
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

        if (_promptEnders is not null)
        {
            foreach (var promptEnder in _promptEnders)
            {
                agent.Instructions.AddPromptEnder(promptEnder);
            }
        }

        if (_agents.ContainsKey(agentDescriptor.Name))
        {
            _logger.LogWarning("Agent with name {agentName} already exists.", agentDescriptor.Name);
            throw new Exception($"Agent with name {agentDescriptor.Name} already exists.");
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

    private void UpdateHandoffs()
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

            agent.Handoffs = agentDescriptor.Handoffs.Select(h => Handoff<TContext>.Create(_agents[h])).ToList();
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
        LoadAgentFromAssembly();
        LoadAgentFromYaml();
        UpdateHandoffs();
        UpdateAgentTools();
    }

    private void AddAgentToMetaAgentHandoffs(string agentName)
    {
        var metaAgentDescriptor = _agentDescriptors["meta_agent"];
        if (metaAgentDescriptor != null)
        {
            metaAgentDescriptor.Handoffs.Add(agentName);
            _agentDescriptors["meta_agent"] = metaAgentDescriptor;
            UpdateHandoffs();
        }
    }

    private void LoadAgentFromAssembly()
    {
        var agentDescriptorType = typeof(IAgentDescriptor);
        var agentDescriptorTypes = _assembliesToScan
            .SelectMany(a => a.GetTypes())
            .Where(t => agentDescriptorType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

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

            AddAgentDescriptor(agentDescriptor);
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
            LoadAgentFromFile(yamlFile);
        }
    }

    // Load yaml agents from a local folder
    public void LoadAgentsFromFolder(string folderPath)
    {
        if (string.IsNullOrEmpty(folderPath) || !Directory.Exists(folderPath))
        {
            _logger.LogWarning("Folder path is null or does not exist: {folderPath}", folderPath);
            return;
        }
        var yamlFiles = Directory.GetFiles(folderPath, "*.yaml", SearchOption.AllDirectories)
            .Concat(Directory.GetFiles(folderPath, "*.yml", SearchOption.AllDirectories));
        foreach (var yamlFile in yamlFiles)
        {
            var agentInfo = LoadAgentFromFile(yamlFile);
            AddAgentToMetaAgentHandoffs(agentInfo.Name);
        }
    }

    private static YamlAgentDescriptor LoadAgentFromYaml(string yamlContent)
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

    private Agent<TContext> LoadAgentFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("YAML file not found", filePath);

            string yamlContent = File.ReadAllText(filePath);

            var agentDescriptor = AgentFactory<TContext>.LoadAgentFromYaml(yamlContent);
            var agent = AddAgentDescriptor(agentDescriptor);
            _logger.LogInformation("Successfully loaded agent descriptor {descriptorName} from file {filePath}.", agentDescriptor.Name, filePath);
            return agent;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load agent descriptor from file {filePath}.", filePath);
            throw;
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

    public List<IAgentDescriptor> GetAllAgentDescriptors()
    {
        return [.. _agentDescriptors.Values];
    }

    public IReadOnlyDictionary<string, IPromptDescriptor> PromptDescriptors =>
       _promptDescriptors.AsReadOnly();
}

