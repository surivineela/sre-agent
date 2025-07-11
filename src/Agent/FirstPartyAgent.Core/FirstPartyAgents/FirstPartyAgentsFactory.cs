using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.MetaAgent.Interfaces;
using FirstPartyAgent.Core.Plugins.Definitions;
using Microsoft.Extensions.AI;
using Agent.Plugins;
using Agent.Plugins.Interface;


namespace FirstPartyAgent.Core.FirstPartyAgents;

public enum FirstPartyMetaAgentNames
{
    Unknown,
    ACAAgent,
}

public class FirstPartyAgentsFactory : IAgentsFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ITimePlugin _timePlugin;

    public FirstPartyAgentsFactory(
        IServiceProvider serviceProvider,
        ITimePlugin timePlugin
        )
    {
        _serviceProvider = serviceProvider;
        _timePlugin = timePlugin;
    }

    public List<AITool> GetSubAgentsAITools(Guid threadGuid, AgentContext context)
    {
        List<AITool> _aiTools = [];
        var subAgentTools = SubAgentDiscovery.GetSubAgentTools(threadGuid, typeof(FirstPartyAgentsFactory).Assembly, _serviceProvider);
        if (subAgentTools?.Count > 0)
        {
            _aiTools.AddRange(subAgentTools);
        }

        var timePluginDefinition = new TimePluginDefinition(_timePlugin);

        _aiTools.AddRange(
            new List<AITool>
            {
                AIFunctionFactory.Create(timePluginDefinition.GetCurrentUtcTime),
            });
        return _aiTools;
    }

    public string GetMetaAgentSystemPrompt()
    {
        var metaAgent = GetMetaAgent();
        string? systemPrompt = null;
        if (metaAgent == FirstPartyMetaAgentNames.ACAAgent)
        {            
            var path = Path.Combine(AppContext.BaseDirectory, nameof(FirstPartyAgent.Core.FirstPartyAgents), "ACA", "ACAAgentSystemPrompt.txt");
            systemPrompt = File.ReadAllText(path);
        }
        if (string.IsNullOrEmpty(systemPrompt))
        {
            throw new InvalidOperationException("System prompt not found for the agent");
        }
        return systemPrompt;
    }

    public string GetIncidentHandlerAgentSystemPrompt(string? agentMode)
    {
        return string.Empty;
    }

    private FirstPartyMetaAgentNames GetMetaAgent()
    {
        var agentName = Environment.GetEnvironmentVariable("AGENT_TYPE_NAME") ?? string.Empty;
        if (agentName?.StartsWith(FirstPartyMetaAgentNames.ACAAgent.ToString(), StringComparison.InvariantCultureIgnoreCase) == true)
        {
            return FirstPartyMetaAgentNames.ACAAgent;
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
        };
        return types;
    }
}
