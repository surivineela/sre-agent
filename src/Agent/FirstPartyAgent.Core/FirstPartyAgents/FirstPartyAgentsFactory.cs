using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.MetaAgent.Interfaces;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.Extensions.AI;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Agent.Plugins;
using FirstPartyAgent.Core.Plugins.Implementation;

namespace FirstPartyAgent.Core.FirstPartyAgents;

public enum FirstPartyMetaAgentNames
{
    Unknown,
    RCAAgent,
}

public class FirstPartyAgentsFactory : IAgentsFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IContainerAppIcMPlugin _containerAppIcMPlugin;
    private readonly ITimePlugin _timePlugin;
    private readonly IManagedClusterPlugin _managedClusterPlugin;
    private readonly IManagedEnvironmentPlugin _managedEnvironmentPlugin;

    public FirstPartyAgentsFactory(
        IServiceProvider serviceProvider,
        IContainerAppIcMPlugin containerAppIcMPlugin,
        ITimePlugin timePlugin,
        IManagedClusterPlugin managedClusterPlugin,
        IManagedEnvironmentPlugin managedEnvironmentPlugin)
    {
        _serviceProvider = serviceProvider;
        _containerAppIcMPlugin = containerAppIcMPlugin;
        _timePlugin = timePlugin;
        _managedClusterPlugin = managedClusterPlugin;
        _managedEnvironmentPlugin = managedEnvironmentPlugin;

    }

    public List<AITool> GetSubAgentsAITools(Guid threadGuid, AgentContext context)
    {
        List<AITool> _aiTools = [];
        var subAgentTools = SubAgentDiscovery.GetSubAgentTools(threadGuid, typeof(FirstPartyAgentsFactory).Assembly, _serviceProvider);
        if (subAgentTools?.Count > 0)
        {
            _aiTools.AddRange(subAgentTools);
        }

        var containerAppIcMPluginDefinition = new ContainerAppIcMPluginDefinition(_containerAppIcMPlugin);
        var timePluginDefinition = new TimePluginDefinition(_timePlugin);
        var managedClusterPluginDefinition = new ManagedClusterPluginDefinition(_managedClusterPlugin);
        var managedEnvironmentPluginDefinition = new ManagedEnvironmentPluginDefinition(_managedEnvironmentPlugin);

        _aiTools.AddRange(
            new List<AITool>
            {
                AIFunctionFactory.Create(managedClusterPluginDefinition.GetManagedClusterInformation),
                AIFunctionFactory.Create(managedClusterPluginDefinition.GetASIPageForManagedCluster),
                AIFunctionFactory.Create(managedEnvironmentPluginDefinition.GetManagedEnvironmentInfo),
                AIFunctionFactory.Create(managedEnvironmentPluginDefinition.GetASIPageForManagedEnvironment),
                AIFunctionFactory.Create(timePluginDefinition.GetCurrentUtcTime),
                // TODO: ideally we should use `GetInitialInvestigationReportAsync` as it minimizes the model context but currently summarization is taking ~45 seconds
                AIFunctionFactory.Create(containerAppIcMPluginDefinition.GetInitialInvestigationReportAsync),
                // AIFunctionFactory.Create(containerAppIcMPluginDefinition.GetIncidentInfo),
                AIFunctionFactory.Create(containerAppIcMPluginDefinition.GetIssueInvestigationTimeRange),
            });
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
        var agentName = Environment.GetEnvironmentVariable("AGENT_TYPE_NAME") ?? string.Empty;
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
            typeof(ManagedClusterPluginDefinition),
            typeof(ManagedEnvironmentPluginDefinition),
            typeof(HealthProbePluginDefinition),
            typeof(NodeAvailabilityPluginDefinition),
            typeof(KustoPluginDefinition),
            typeof(ContainerAppEnvoyPluginDefinition),
            typeof(ContainerAppCorednsPluginDefinition),
        };
        return types;
    }
}
