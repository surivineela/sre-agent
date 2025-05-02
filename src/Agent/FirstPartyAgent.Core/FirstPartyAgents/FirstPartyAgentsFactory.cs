using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.MetaAgent.Interfaces;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.Extensions.AI;

namespace FirstPartyAgent.Core.FirstPartyAgents;
public class FirstPartyAgentsFactory : IAgentsFactory
{
    private readonly IServiceProvider _serviceProvider;

    public FirstPartyAgentsFactory(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public List<AITool> GetSubAgentsAITools(Guid threadGuid, AgentContext context)
    {
        List<AITool> _aiTools = [];
        var subAgentTools = SubAgentDiscovery.GetSubAgentTools(threadGuid, typeof(FirstPartyAgentsFactory).Assembly, _serviceProvider);
        if (subAgentTools?.Count > 0)
        {
            _aiTools.AddRange(subAgentTools);
        }
        return _aiTools;
    }

    public string GetMetaAgentSystemPrompt()
    {
        var agentName = GetAgentName();
        string? systemPrompt = null;
        if (string.Equals(agentName, "RCAAgent", StringComparison.InvariantCultureIgnoreCase))
        {
            var path = Path.Combine("..", "FirstPartyAgent.Core", nameof(FirstPartyAgents), "ACA", "RCAAgentSystemPrompt.txt");
            systemPrompt = File.ReadAllText(path);
        }
        if (string.IsNullOrEmpty(systemPrompt))
        {
            throw new InvalidOperationException("System prompt not found for the agent");
        }
        return systemPrompt;
    }

    private string GetAgentName()
    {
        return Environment.GetEnvironmentVariable("AGENT_NAME") ?? string.Empty;
    }

    public List<Type> GetRequiredSubAgentPluginDefinitionTypes()
    {
        // TODO: make it generic
        var types = new List<Type>
        {
            typeof(HelloWorldPluginDefinition),
            //Plugins requires by quota agent.
            //TODO: going to make it read from env variable
            typeof(ContainerAppsPluginDefinition),
            typeof(IcmPluginDefinition),
            typeof(ContainerAppRevisionPluginDefinition),
            typeof(KustoPluginDefinition),
            typeof(ContainerAppEnvoyPluginDefinition),
            typeof(ContainerAppCorednsPluginDefinition),
        };
        return types;
    }
}
