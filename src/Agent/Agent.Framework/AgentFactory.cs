using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Framework.Examples;

public interface IAgentDescriptor
{
    public string Name { get; set; }

    public string Instructions { get; set; }

    public string? HandoffDescription { get; set; }
    public List<string> Handoffs { get; set; }
    public List<string> Tools { get; set; }
}

public interface IToolsRepository
{
    public AIFunction FindAiFunction(string name);
}

public class AgentFactory<TContext> where TContext : class
{

    // A map from Agent name -> Agent descriptor
    private readonly IDictionary<string, Agent<TContext>> _agents;
    private readonly IDictionary<string, IAgentDescriptor> _agentDescriptors;
    private readonly ILogger<AgentFactory<TContext>> _logger;
    private readonly IToolsRepository _toolsRepository;

    public AgentFactory(ILogger<AgentFactory<TContext>> logger, IToolsRepository toolsRepository)
    {
        _toolsRepository = toolsRepository;
        _logger = logger;
        _agents = new Dictionary<string, Agent<TContext>>();
        _agentDescriptors = new Dictionary<string, IAgentDescriptor>();
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
            _logger.LogError($"Agent descriptor {agentDescriptor.GetType().Name} does not have a name.");
            return false;
        }

        if (string.IsNullOrEmpty(agentDescriptor.Instructions))
        {
            _logger.LogError($"Agent descriptor {agentDescriptor.Name} does not have instructions.");
            return false;
        }

        if (_agents.ContainsKey(agentDescriptor.Name))
        {
            _logger.LogError($"Agent descriptor {agentDescriptor.Name} already exists.");
            return false;
        }

        return true;
    }

    private void InitializeAgents()
    {
        var agentDescriptorType = typeof(IAgentDescriptor);
        var agentDescriptorTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName()?.Name?.StartsWith("Agent.Framework") == true) // Added null checks
            .SelectMany(a => a.GetTypes())
            .Where(t => agentDescriptorType.IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract);

        if (!agentDescriptorTypes.Any())
        {
            _logger.LogError("No agent descriptors found.");
            return;
        }

        foreach (var agentType in agentDescriptorTypes)
        {
            if (Activator.CreateInstance(agentType) is not IAgentDescriptor agentDescriptor)
            {
                _logger.LogError($"Failed to create an instance of {agentType.FullName}.");
                continue;
            }

            if (!ValidateAgentDescriptor(agentDescriptor))
            {
                _logger.LogError($"Agent descriptor {agentDescriptor.GetType().Name} is not valid.");
                continue;
            }

            var agent = new Agent<TContext>(agentDescriptor.Name)
            {
                Instructions = agentDescriptor.Instructions,
                HandoffDescription = agentDescriptor.HandoffDescription,
                Handoffs = [], // On first pass, we will not specify handoffs because there will be circular references and the target agent may not be initialized yet
                AutoTools = agentDescriptor.Tools.Select(t => _toolsRepository.FindAiFunction(t)).ToList()
            };

            _agents[agentDescriptor.Name] = agent;
            _agentDescriptors[agentDescriptor.Name] = agentDescriptor;
        }

        foreach (var agent in _agents.Values)
        {
            var agentDescriptor = _agentDescriptors[agent.Name];
            foreach (var handoff in agentDescriptor.Handoffs)
            {
                if (!_agents.ContainsKey(handoff))
                {
                    var error = $"Agent descriptor {agentDescriptor.Name} has a handoff to {handoff} but it does not exist.";
                    _logger.LogError(error);
                    throw new Exception(error);
                }
            }

            agent.Handoffs = agentDescriptor.Handoffs.Select(h => Handoff<TContext>.Create(_agents[h])).ToList();
        }
    }

    public Agent<TContext> GetAgent(string name)
    {
        return _agents.TryGetValue(name, out var agent) ? agent : throw new KeyNotFoundException($"Agent {name} not found.");
    }
}
