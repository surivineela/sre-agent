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
    private readonly ILogger<AgentFactory<TContext>> _logger;
    private readonly IToolFactory _toolFactory;
    private readonly IEnumerable<Assembly> _assembliesToScan;
    private readonly string _agentsYamlDirectory;

    public AgentFactory(
        ILogger<AgentFactory<TContext>> logger,
        IToolFactory toolFactory,
        IEnumerable<Assembly> assembliesToScan,
        string agentsYamlDirectory
    )
    {
        _toolFactory = toolFactory;
        _logger = logger;
        _assembliesToScan = assembliesToScan;
        _agentsYamlDirectory = agentsYamlDirectory;
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

        if (agentDescriptor.AutoTools.Any(toolName => !_toolFactory.HasAIFunction(toolName)))
        {
            _logger.LogError("Agent descriptor {descriptorName} has auto tools that do not exist in the tool factory.", agentDescriptor.Name);
            return false;
        }

        if (agentDescriptor.ManualTools.Any(toolName => !_toolFactory.HasAIFunction(toolName)))
        {
            _logger.LogError("Agent descriptor {descriptorName} has manual tools that do not exist in the tool factory.", agentDescriptor.Name);
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
            Handoffs = [], // Will be populated later to avoid circular references
            AutoTools = agentDescriptor.AutoTools.Select(_toolFactory.FindAIFunction).ToList(),
            ManualTools = agentDescriptor.ManualTools.Select(_toolFactory.FindAIFunction).ToList(), // Note the tools will be created again with ThreadId in the reasoning loop
        };

        agent.Instructions
            .WithHandoffInstructions()
            .WithFormattingGuidelines();

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
        var yamlFiles = Directory.GetFiles(_agentsYamlDirectory, "*.yaml", SearchOption.AllDirectories)
                       .Concat(Directory.GetFiles(_agentsYamlDirectory, "*.yml", SearchOption.AllDirectories));

        foreach (var yamlFile in yamlFiles)
        {
            LoadAgentFromFile(yamlFile);
        }
    }

    public IAgentDescriptor LoadAgentFromYaml(string yamlContent)
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
                _logger.LogInformation($"Successfully loaded agent descriptor {agentDescriptor.Name} from file {filePath}.");
            }
            else
            {
                _logger.LogError($"Failed to load agent descriptor from file {filePath}.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to load agent descriptor from file {filePath}.");
            throw;
        }
    }

    public Agent<TContext> GetAgent(string name)
    {
        var agentFound = _agents.TryGetValue(name, out var agent);
        if (!agentFound || agent is null)
        {
            _logger.LogError($"Agent {name} not found.");
            throw new KeyNotFoundException($"Agent {name} not found.");
        }

        return agent;
    }
}
