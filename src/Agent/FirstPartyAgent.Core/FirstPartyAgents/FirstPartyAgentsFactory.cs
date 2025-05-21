using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.MetaAgent.Interfaces;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.Extensions.AI;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Agent.Plugins;
using FirstPartyAgent.Core.Plugins.Implementation;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.FirstPartyAgents;

public enum FirstPartyMetaAgentNames
{
    Unknown,
    ACAAgent,
}

public class FirstPartyAgentsFactory : IAgentsFactory
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IContainerAppIcMPlugin _containerAppIcMPlugin;
    private readonly ITimePlugin _timePlugin;
    private readonly IManagedClusterPlugin _managedClusterPlugin;
    private readonly IManagedEnvironmentPlugin _managedEnvironmentPlugin;
    private readonly IContainerAppsPlugin _containerAppsPlugin;

    public FirstPartyAgentsFactory(
        IServiceProvider serviceProvider,
        IContainerAppIcMPlugin containerAppIcMPlugin,
        ITimePlugin timePlugin,
        IManagedClusterPlugin managedClusterPlugin,
        IManagedEnvironmentPlugin managedEnvironmentPlugin,
        IContainerAppsPlugin containerAppsPlugin
        )
    {
        _serviceProvider = serviceProvider;
        _containerAppIcMPlugin = containerAppIcMPlugin;
        _timePlugin = timePlugin;
        _managedClusterPlugin = managedClusterPlugin;
        _managedEnvironmentPlugin = managedEnvironmentPlugin;
        _containerAppsPlugin = containerAppsPlugin;
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
        var containerAppsPluginDefinition = new ContainerAppsPluginDefinition(_containerAppsPlugin);

        _aiTools.AddRange(
            new List<AITool>
            {
                AIFunctionFactory.Create(managedClusterPluginDefinition.GetASIPageForManagedCluster),
                AIFunctionFactory.Create(managedEnvironmentPluginDefinition.GetManagedEnvironmentInfo),
                AIFunctionFactory.Create(managedEnvironmentPluginDefinition.GetChangesInManagedEnvironment),
                AIFunctionFactory.Create(managedEnvironmentPluginDefinition.GetASIPageForManagedEnvironment),
                AIFunctionFactory.Create(timePluginDefinition.GetCurrentUtcTime),
                AIFunctionFactory.Create(containerAppIcMPluginDefinition.GetInitialInvestigationReportAsync),
                AIFunctionFactory.Create(containerAppIcMPluginDefinition.GetIssueInvestigationTimeRange),
                AIFunctionFactory.Create(containerAppsPluginDefinition.GetSubscriptionDetail),
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
            typeof(ContainerAppsPluginDefinition),
            typeof(ContainerAppQuotaPluginDefinition),
            typeof(ContainerAppRevisionPluginDefinition),
            typeof(ContainerAppJobsPluginDefinition),
            typeof(ManagedClusterPluginDefinition),
            typeof(ManagedEnvironmentPluginDefinition),
            typeof(HealthProbePluginDefinition),
            typeof(NodeAvailabilityPluginDefinition),
            typeof(ACAKustoPluginDefinition),
            typeof(ContainerAppEnvoyPluginDefinition),
            typeof(ContainerAppCorednsPluginDefinition),
            typeof(ContainerAppCustomerLogsPluginDefinition),
            typeof(ContainerAppCustomerMetricsPluginDefinition),
        };
        return types;
    }
}
