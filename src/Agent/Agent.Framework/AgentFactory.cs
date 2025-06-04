// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Framework;


public interface IAgentFactory<TContext>
    where TContext : class
{
    public Agent<TContext> GetAgent(string name);
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

    public AgentFactory(
        ILogger<AgentFactory<TContext>> logger,
        IToolFactory<TContext> toolFactory,
        IEnumerable<Assembly> assembliesToScan,
        string? agentsYamlDirectory = null,
        string? commonPromptsYamlDirectory = null
    )
    {
        _toolFactory = toolFactory;
        _logger = logger;
        _assembliesToScan = assembliesToScan;
        _agentsYamlDirectory = agentsYamlDirectory;
        _commonPromptsYamlDirectory = commonPromptsYamlDirectory;
        InitializeAgents();
    }

    private bool ValidateAgentDescriptor(IAgentDescriptor? agentDescriptor)
    {
        if (agentDescriptor is null)
        {
            _logger.LogError("Agent descriptor is null.");
            return false;
        }

        if (string.IsNullOrEmpty(agentDescriptor.Name))
        {
            _logger.LogError("Agent descriptor {descriptorType} does not have a name.", agentDescriptor.GetType().Name);
            return false;
        }

        if (string.IsNullOrEmpty(agentDescriptor.Instructions))
        {
            _logger.LogError("Agent descriptor {descriptorName} does not have instructions.", agentDescriptor.Name);
            return false;
        }

        if (_agents.ContainsKey(agentDescriptor.Name))
        {
            _logger.LogError("Agent descriptor {descriptorName} already exists.", agentDescriptor.Name);
            return false;
        }

        if (agentDescriptor.Tools.Any(toolName => !_toolFactory.HasTool(toolName)))
        {
            var missingTools = agentDescriptor.Tools.Where(toolName => !_toolFactory.HasTool(toolName)).ToList();
            _logger.LogError("Agent descriptor {descriptorName} has tools that do not exist in the tool factory: {missingTools}",
                agentDescriptor.Name, string.Join(", ", missingTools));
            return false;
        }

        return true;
    }

    private bool AddAgentDescriptor(IAgentDescriptor agentDescriptor)
    {
        if (!ValidateAgentDescriptor(agentDescriptor))
        {
            _logger.LogError("Agent descriptor {descriptorType} is not valid.", agentDescriptor?.GetType().Name ?? "null");
            return false;
        }

        var agent = new Agent<TContext>(agentDescriptor.Name)
        {
            Instructions = agentDescriptor.Instructions,
            HandoffDescription = agentDescriptor.HandoffDescription,
            MaxReflectionCount = agentDescriptor.MaxReflectionCount,
            CriticPromptPath = Path.Join(AppContext.BaseDirectory, agentDescriptor.CriticPromptPath),
            CustomReflectionNote = agentDescriptor.CustomReflectionNote,
            Handoffs = [], // Will be populated later to avoid circular references
            FactoryTools = agentDescriptor.Tools,
            AllowParallelToolCalls = agentDescriptor.AllowParallelToolCalls,
        };

        if (agentDescriptor.Temperature is not null)
        {
            agent.Temperature = agentDescriptor.Temperature.Value;
        }

        agent.Instructions
            .WithHandoffInstructions()
            .WithFormattingGuidelines();

        foreach (var commonPromptName in agentDescriptor.CommonPrompts)
        {
            if (!_promptDescriptors.TryGetValue(commonPromptName, out var commonPrompt))
            {
                _logger.LogWarning("Agent descriptor {descriptorName} has a common prompt {commonPromptName} that does not exist.",
                    agentDescriptor.Name, commonPromptName);

                return false;
            }

            agent.Instructions.AddCommonPrompt(commonPrompt.Prompt);
        }

        if (_agents.ContainsKey(agentDescriptor.Name))
        {
            _logger.LogWarning("Agent with name {agentName} already exists.", agentDescriptor.Name);
            return false;
        }

        _agents[agentDescriptor.Name] = agent;
        _agentDescriptors[agentDescriptor.Name] = agentDescriptor;
        return true;
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

    private void InitializeAgents()
    {
        LoadCommonPromptsFromAssembly();
        LoadCommonPromptsFromYaml();
        LoadAgentFromAssembly();
        LoadAgentFromYaml();
        UpdateHandoffs();
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

            if (AddAgentDescriptor(agentDescriptor))
            {
                _logger.LogInformation("Successfully loaded agent descriptor '{descriptorName}' from assembly '{assemblyName}'.", agentType.Name, agentType.Assembly.GetName().Name);
            }
            else
            {
                _logger.LogError("Failed to load agent descriptor '{descriptorName}' from assembly '{assemblyName}'.", agentType.Name, agentType.Assembly.GetName().Name);
            }
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

    private IAgentDescriptor LoadAgentFromYaml(string yamlContent)
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

    private void LoadAgentFromFile(string filePath)
    {
        try
        {
            if (!File.Exists(filePath))
                throw new FileNotFoundException("YAML file not found", filePath);

            string yamlContent = File.ReadAllText(filePath);

            var agentDescriptor = LoadAgentFromYaml(yamlContent);
            if (AddAgentDescriptor(agentDescriptor))
            {
                _logger.LogInformation("Successfully loaded agent descriptor {descriptorName} from file {filePath}.", agentDescriptor.Name, filePath);
            }
            else
            {
                _logger.LogError("Failed to load agent descriptor from file {filePath}.", filePath);
            }
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

    private IPromptDescriptor LoadCommonPromptFromYaml(string yamlContent)
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
}

