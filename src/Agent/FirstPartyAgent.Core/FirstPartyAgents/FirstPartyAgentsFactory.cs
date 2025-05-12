using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.MetaAgent.Interfaces;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.Extensions.AI;

namespace FirstPartyAgent.Core.FirstPartyAgents;

public enum FirstPartyMetaAgentNames
{
    Unknown,
    RCAAgent,
}

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
        var metaAgent = GetMetaAgent();
        string? systemPrompt = null;
        if (metaAgent == FirstPartyMetaAgentNames.RCAAgent)
        {            
            var path = Path.Combine(AppContext.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartyAgents), "ACA", "RCAAgentSystemPrompt.txt");
            systemPrompt = File.ReadAllText(path);
        }
        if (string.IsNullOrEmpty(systemPrompt))
        {
            throw new InvalidOperationException("System prompt not found for the agent");
        }
        return systemPrompt;
    }

    private FirstPartyMetaAgentNames GetMetaAgent()
    {
        var agentName = Environment.GetEnvironmentVariable("AGENT_NAME") ?? string.Empty;
        if (agentName?.StartsWith(FirstPartyMetaAgentNames.RCAAgent.ToString(), StringComparison.InvariantCultureIgnoreCase) == true)
        {
            return FirstPartyMetaAgentNames.RCAAgent;
        }
        return FirstPartyMetaAgentNames.Unknown;
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
            typeof(ContainerAppQuotaPluginDefinition),
            typeof(ContainerAppRevisionPluginDefinition),
            typeof(KustoPluginDefinition),
            typeof(ContainerAppEnvoyPluginDefinition),
            typeof(ContainerAppCorednsPluginDefinition),
            typeof(IcmPluginDefinition),
            typeof(ContainerAppIcMPluginDefinition),
        };
        return types;
    }
}
