using Agent.Plugins.Definitions;
using Agent.Plugins;
using Agent.Runtime.HelperAgents;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Agent.Plugins.Interface;

namespace Agent.Tests.Common.ScenarioTestHelpers;
public static class AKSTestHelpers
{
    public static void AddPluginDefinitions(this IServiceCollection services)
    {
        services.AddSingleton<MetricsPluginDefinition>();
        services.AddSingleton<ArmPluginDefinition>();
        services.AddSingleton<RecordActionsPluginDefinition>();
        services.AddSingleton<ControlFlowPluginDefinition>();
        services.AddSingleton<ChartPluginDefinition>();
        services.AddSingleton<GraphDBPluginDefinition>();
        services.AddSingleton<TimePluginDefinition>();
        services.AddSingleton<HelperAgentsPluginDefinition>();
        services.AddSingleton<DiagnosisAgent>();

        services.AddSingleton<MIConfigurationCheckPluginDefinition>()
                .AddSingleton(sp => new Mock<IMIConfigurationCheckPlugin>().Object)
                .AddSingleton<GithubWorkflowTriggerPluginDefinition>()
                .AddSingleton(sp => new Mock<IGithubWorkflowTriggerPlugin>().Object)
                .AddSingleton<RemediationPluginDefinition>()
                .AddSingleton(sp => new Mock<IRemediationPlugin>().Object)
                .AddSingleton<AppIdentityUpdatePluginDefinition>()
                .AddSingleton(sp => new Mock<IAppIdentityUpdatePlugin>().Object)
                .AddSingleton<ContainerAppPluginDefinition>()
                .AddSingleton(sp => new Mock<IContainerAppPlugin>().Object)
                .AddSingleton<ReliabilityPluginDefinition>()
                .AddSingleton(sp => new Mock<IReliabilityPlugin>().Object)
                .AddSingleton<IncidentPluginDefinition>()
                .AddSingleton(sp => new Mock<IIncidentPlugin>().Object)
                .AddSingleton(sp => new Mock<INSGRulePlugin>().Object)
                .AddSingleton<NSGRulePluginDefinition>()
                .AddSingleton(sp => new Mock<IGithubIssuePlugin>().Object)
                .AddSingleton<GitHubIssuePluginDefinition>();

    }
}
