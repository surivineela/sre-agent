using Microsoft.Extensions.Logging;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Agent.Framework;

public interface IAgentDescriptor
{
    public string Name { get; set; }

    public string Instructions { get; set; }

    public string? HandoffDescription { get; set; }
    public List<string> Handoffs { get; set; }
    public List<string> Tools { get; set; }
}

public interface IAgentFactory<TContext>
    where TContext : class
{
    public Agent<TContext> GetAgent(string name);
}

public class AgentFactory<TContext> : IAgentFactory<TContext>
    where TContext : class
{
    // A map from Agent name -> Agent descriptor
    private readonly IDictionary<string, Agent<TContext>> _agents;
    private readonly IDictionary<string, IAgentDescriptor> _agentDescriptors;
    private readonly ILogger<AgentFactory<TContext>> _logger;
    private readonly IToolFactory _toolFactory;

    public AgentFactory(ILogger<AgentFactory<TContext>> logger, IToolFactory toolsRepository)
    {
        _toolFactory = toolsRepository;
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

        if (agentDescriptor.Tools.Any(toolName => !_toolFactory.HasAIFunction(toolName)))
        {
            _logger.LogError($"Agent descriptor {agentDescriptor.Name} has tools that do not exist in the tool factory.");
            return false;
        }

        return true;
    }

    private bool AddAgentDescriptor(IAgentDescriptor agentDescriptor)
    {
        if (!ValidateAgentDescriptor(agentDescriptor))
        {
            _logger.LogError($"Agent descriptor {agentDescriptor?.GetType().Name ?? "null"} is not valid.");
            return false;
        }

        var agent = new Agent<TContext>(agentDescriptor.Name)
        {
            Instructions = agentDescriptor.Instructions,
            HandoffDescription = agentDescriptor.HandoffDescription,
            Handoffs = [], // Will be populated later to avoid circular references
            AutoTools = [], // Will be created later when GetAgent is called
        };

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
                    throw new Exception(error);
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
        var agentDescriptorTypes = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.GetName()?.Name?.StartsWith("Agent.") == true) // Added null checks
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
            if (agentDescriptor.GetType()?.Name == "YamlAgentDescriptor")
            {
                _logger.LogDebug("Skipping YamlAgentDescriptor type as it's just for parser.");
                continue;
            }

            AddAgentDescriptor(agentDescriptor);
        }
    }

    private void LoadAgentFromYaml()
    {
        var agentsFolder = Path.Combine(AppContext.BaseDirectory, "AgentsV2");
        var yamlFiles = Directory.GetFiles(agentsFolder, "*.yaml", SearchOption.AllDirectories)
                       .Concat(Directory.GetFiles(agentsFolder, "*.yml", SearchOption.AllDirectories));

        foreach (var yamlFile in yamlFiles)
        {
            try
            {
                LoadAgentFromFile(yamlFile);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to load agent from {yamlFile}");
            }
        }
    }

    public void LoadAgentFromYaml(string yamlContent)
    {
        try
        {
            var agentDescriptor = AgentDescriptorParser.ParseFromYaml(yamlContent);
            if (AddAgentDescriptor(agentDescriptor))
            {
                _logger.LogInformation($"Successfully loaded agent {agentDescriptor.Name} from YAML.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load agent from YAML content.");
            throw;
        }
    }

    public void LoadAgentFromFile(string filePath)
    {
        try
        {
            var agentDescriptor = AgentDescriptorParser.ParseFromFile(filePath);
            if (AddAgentDescriptor(agentDescriptor))
            {
                _logger.LogInformation($"Successfully loaded agent {agentDescriptor.Name} from file {filePath}.");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load agent from file {FilePath}.", filePath);
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

        return new Agent<TContext>(name)
        {
            Instructions = agent.Instructions,
            HandoffDescription = agent.HandoffDescription,
            AutoTools = _agentDescriptors[name].Tools
                .Select(_toolFactory.FindAIFunction)
                .ToList(),
            ManualTools = agent.ManualTools,
            Handoffs = agent.Handoffs,
            Hooks = agent.Hooks
        };
    }
}

public class AgentDescriptorParser
{
    public static IAgentDescriptor ParseFromYaml(string yamlContent)
    {
        try
        {
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var agentDescriptor = deserializer.Deserialize<YamlAgentDescriptor>(yamlContent);

            // Validate required fields
            if (string.IsNullOrEmpty(agentDescriptor.Name))
                throw new ArgumentException("Name field is required in YAML");
            if (string.IsNullOrEmpty(agentDescriptor.SystemPrompt))
                throw new ArgumentException("SystemPrompt field is required in YAML");

            agentDescriptor.Instructions = Prompt.PromptWithHandoffInstructions(agentDescriptor.SystemPrompt);
            return agentDescriptor;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to parse YAML into IAgentDescriptor", ex);
        }
    }

    public static IAgentDescriptor ParseFromFile(string filePath)
    {
        if (!File.Exists(filePath))
            throw new FileNotFoundException("YAML file not found", filePath);

        string yamlContent = File.ReadAllText(filePath);
        return ParseFromYaml(yamlContent);
    }

    private class YamlAgentDescriptor : IAgentDescriptor
    {
        [YamlMember(Alias = "name")]
        public string Name { get; set; } = string.Empty;

        [YamlMember(Alias = "system_prompt")]
        public string SystemPrompt { get; set; } = string.Empty;

        public string Instructions { get; set; } = string.Empty;

        [YamlMember(Alias = "handoff_description")]
        public string? HandoffDescription { get; set; }

        [YamlMember(Alias = "handoffs")]
        public List<string> Handoffs { get; set; } = ["meta_agent"];

        [YamlMember(Alias = "tools")]
        public List<string> Tools { get; set; } = [];
    }
}
