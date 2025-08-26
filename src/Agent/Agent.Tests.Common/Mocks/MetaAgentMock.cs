using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Logging;
using Agent.Plugins.Interface;
using Agent.Plugins.Services.Interfaces;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;

namespace Agent.Tests.Common.Mocks;
public static class MetaAgentMock
{
    public static ThirdPartyAgentsFactory GetMockedThirdPartAgentsFactory(
        ILogger<ThirdPartyAgentsFactory>? logger = null,
        McpToolsRepository? mcpToolsRepository = null,
        IServiceProvider? serviceProvider = null,
        IChartPlugin? chartplugin = null,
        DashboardSettings? dashboardSettings = null,
        //IMetaAgentAppServiceRemediationPlugin? appServiceRemediationPlugin = null,
        //IMetaAgentLocalAuthPlugin? localAuthPlugin = null,
        IAppServicePlugin? appServicePlugin = null,
        IContainerAppPlugin? containerAppPlugin = null,
        IFunctionAppsPlugin? functionsAppPlugin = null,
        IKubePlugin? kubePlugin = null,
        IGithubIssuePlugin? githubIssuePlugin = null,
        IGraphDBPlugin? graphDBPlugin = null,
        //IMetaAgentAppReliabilityPlugin? appReliabilityPlugin = null,
        IAppCodeAnalysisPlugin? appCodePlugin = null,
        ICpuAnalysisPlugin? cpuPlugin = null,
        IMetricsPlugin? metricsPlugin = null,
        IPagerDutyIncidentPlugin? incidentPlugin = null,
        InstanceManagementSettings? instanceManagementSettings = null,
        IAzureMonitorMetricsPlugin? azureMonitorMetricsPlugin = null,
        IDiagnosticsPlugin? diagnosticsPlugin = null,
        IArmPlugin? armPlugin = null,
        ISearchPlugin? searchPlugin = null,
        IRemediationPlugin? remediationPlugin = null,
        IAzureDevOpsWorkItemPlugin? azureDevOpsWorkItemPlugin = null,
        ISourceCodeAnalysisPlugin? sourceCodeAnalysisPlugin = null, // Added parameter
        IGraphService? graphServicePlugin = null
        ) // Added parameter
    {
        return new ThirdPartyAgentsFactory(
            mcpToolsRepository ?? Mock.Of<McpToolsRepository>(),
            serviceProvider ?? new ServiceCollection().BuildServiceProvider(),

            chartplugin ?? Mock.Of<IChartPlugin>(),
            //appServiceRemediationPlugin ?? Mock.Of<IMetaAgentAppServiceRemediationPlugin>(),
            //localAuthPlugin ?? Mock.Of<IMetaAgentLocalAuthPlugin>(),
            appServicePlugin ?? Mock.Of<IAppServicePlugin>(),
            containerAppPlugin ?? Mock.Of<IContainerAppPlugin>(),
            functionsAppPlugin ?? Mock.Of<IFunctionAppsPlugin>(),
            kubePlugin ?? Mock.Of<IKubePlugin>(),
            githubIssuePlugin ?? Mock.Of<IGithubIssuePlugin>(),
            graphDBPlugin ?? Mock.Of<IGraphDBPlugin>(),
            //appReliabilityPlugin ?? Mock.Of<IMetaAgentAppReliabilityPlugin>(),
            Mock.Of<IConnectedIntegrationsPlugin>(),            
            appCodePlugin ?? Mock.Of<IAppCodeAnalysisPlugin>(),
            diagnosticsPlugin ?? Mock.Of<IDiagnosticsPlugin>(),
            metricsPlugin ?? Mock.Of<IMetricsPlugin>(),
            instanceManagementSettings ?? Mock.Of<InstanceManagementSettings>(),
            incidentPlugin ?? Mock.Of<IPagerDutyIncidentPlugin>(),
            azureMonitorMetricsPlugin ?? Mock.Of<IAzureMonitorMetricsPlugin>(),
            armPlugin ?? Mock.Of<IArmPlugin>(),// Added argument
            searchPlugin ?? Mock.Of<ISearchPlugin>(),
            remediationPlugin ?? Mock.Of<IRemediationPlugin>(),
            azureDevOpsWorkItemPlugin ?? Mock.Of<IAzureDevOpsWorkItemPlugin>(),
            sourceCodeAnalysisPlugin ?? Mock.Of<ISourceCodeAnalysisPlugin>(), // Added argument
            graphServicePlugin ?? Mock.Of<IGraphService>()
        );
    }

    public static MetaAgent GetMockedMetaAgent(
        IChatClient chatClient,
        IAgentsFactory agentsFactory,
        ILogger<MetaAgent>? logger = null,
        ThreadService? threadService = null,
        IThreadRepository? threadRepository = null,
        McpToolsRepository? mcpToolsRepository = null
        )
    {

        return new MetaAgent(
            chatClient,
            agentsFactory,
            logger ?? Mock.Of<ILogger<MetaAgent>>(),
            Mock.Of<CustomerLogger>(),
            threadService ?? Mock.Of<ThreadService>(),
            threadRepository ?? Mock.Of<IThreadRepository>()
        );
    }
}
