using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Plugins.Definitions;
using Agent.Plugins;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Microsoft.Extensions.Logging;
using Agent.Core.Models.Api.v1;

namespace Agent.Tests.Common.Mocks;
public static class MetaAgentMock
{
    public static ThirdPartyAgentsFactory GetMockedThirdPartAgentsFactory(
    ILogger<ThirdPartyAgentsFactory>? logger = null,
    McpToolsRepository? mcpToolsRepository = null,
    IServiceProvider? serviceProvider = null,

    IChartPlugin? chartplugin = null,
    DashboardSettings? dashboardSettings = null,
    IMetaAgentManagedIdentityMigrationPlugin? managedIdentityMigrationPlugin = null,
    IMetaAgentTlsBestPracticesPlugin? tlsBestPracticesPlugin = null,
    //IMetaAgentAppServiceRemediationPlugin? appServiceRemediationPlugin = null,
    IMetaAgentContainerAppsRemediationPlugin? containerAppsRemediationPlugin = null,
    //IMetaAgentLocalAuthPlugin? localAuthPlugin = null,
    IMetaAgentKubernetesAgentPlugin? kubernetesAgentPlugin = null,
    IMetaAgentAksQaAgentPlugin aksQaAgentPlugin = null,
    IAppServicePlugin? appServicePlugin = null,
    IContainerAppPlugin? containerAppPlugin = null,
    IFunctionAppsPlugin functionsAppPlugin = null,
    IKubePlugin? kubePlugin = null,
    IGithubIssuePlugin? githubIssuePlugin = null,
    IGraphDBPlugin? graphDBPlugin = null,
    //IMetaAgentAppReliabilityPlugin? appReliabilityPlugin = null,
    IMetaAgentWebAppDownPlugin? webAppDownPlugin = null,

    IMetaAgentVmRdpInvestigatorPlugin? vmRdpInvestigatorPlugin = null,
    IMetaAgentFunctionAppConnectivityPlugin? functionAppConnectivityPlugin = null,
    IMetaAgentSqlDbQueryPerfPlugin? sqlDbQueryPerfPlugin = null,
    IMetaAgentAppCodeAnalysisPlugin appCodeAnalysisPlugin = null,
    IMetaAgentCPUAnalysisPlugin cpuAnalysisPlugin = null,
    IAppCodeAnalysisPlugin appCodePlugin = null,
    ICpuAnalysisPlugin cpuPlugin = null,
    IMetricsPlugin metricsPlugin = null,
    IIncidentPlugin incidentPlugin = null,
    IMetaAgentFunctionAppExecutionFailuresAgentPlugin? functionAppExecutionFailuresAgentPlugin = null,
    InstanceManagementSettings instanceManagementSettings = null,
    IAzureMonitorMetricsPlugin? azureMonitorMetricsPlugin = null,
    IMetaAgentFunctionAppDiagnosticsPlugin? functionAppDiagnosticsPlugin = null,
    IArmPlugin? armPlugin = null, // Added parameter
    ISearchPlugin searchPlugin = null)
    {

        return new ThirdPartyAgentsFactory(
            logger ?? Mock.Of<ILogger<ThirdPartyAgentsFactory>>(),
            mcpToolsRepository ?? Mock.Of<McpToolsRepository>(),
            serviceProvider ?? new ServiceCollection().BuildServiceProvider(),

            chartplugin ?? Mock.Of<IChartPlugin>(),
            managedIdentityMigrationPlugin ?? Mock.Of<IMetaAgentManagedIdentityMigrationPlugin>(),
            tlsBestPracticesPlugin ?? Mock.Of<IMetaAgentTlsBestPracticesPlugin>(),
            //appServiceRemediationPlugin ?? Mock.Of<IMetaAgentAppServiceRemediationPlugin>(),
            containerAppsRemediationPlugin ?? Mock.Of<IMetaAgentContainerAppsRemediationPlugin>(),
            //localAuthPlugin ?? Mock.Of<IMetaAgentLocalAuthPlugin>(),
            kubernetesAgentPlugin ?? Mock.Of<IMetaAgentKubernetesAgentPlugin>(),
            aksQaAgentPlugin ?? Mock.Of<IMetaAgentAksQaAgentPlugin>(),
            appServicePlugin ?? Mock.Of<IAppServicePlugin>(),
            containerAppPlugin ?? Mock.Of<IContainerAppPlugin>(),
            functionsAppPlugin ?? Mock.Of<IFunctionAppsPlugin>(),
            kubePlugin ?? Mock.Of<IKubePlugin>(),
            githubIssuePlugin ?? Mock.Of<IGithubIssuePlugin>(),
            graphDBPlugin ?? Mock.Of<IGraphDBPlugin>(),
            //appReliabilityPlugin ?? Mock.Of<IMetaAgentAppReliabilityPlugin>(),
            webAppDownPlugin ?? Mock.Of<IMetaAgentWebAppDownPlugin>(),
            vmRdpInvestigatorPlugin ?? Mock.Of<IMetaAgentVmRdpInvestigatorPlugin>(),
            functionAppConnectivityPlugin ?? Mock.Of<IMetaAgentFunctionAppConnectivityPlugin>(),
            sqlDbQueryPerfPlugin ?? Mock.Of<IMetaAgentSqlDbQueryPerfPlugin>(),
            Mock.Of<IConnectedIntegrationsPlugin>(),
            appCodeAnalysisPlugin ?? Mock.Of<IMetaAgentAppCodeAnalysisPlugin>(),
            cpuAnalysisPlugin ?? Mock.Of<IMetaAgentCPUAnalysisPlugin>(),
            appCodePlugin ?? Mock.Of<IAppCodeAnalysisPlugin>(),
            cpuPlugin ?? Mock.Of<ICpuAnalysisPlugin>(),
            metricsPlugin ?? Mock.Of<IMetricsPlugin>(),
            instanceManagementSettings ?? Mock.Of<InstanceManagementSettings>(),
            incidentPlugin ?? Mock.Of<IIncidentPlugin>(),
            functionAppExecutionFailuresAgentPlugin ?? Mock.Of<IMetaAgentFunctionAppExecutionFailuresAgentPlugin>(),
            azureMonitorMetricsPlugin ?? Mock.Of<IAzureMonitorMetricsPlugin>(),
            functionAppDiagnosticsPlugin ?? Mock.Of<IMetaAgentFunctionAppDiagnosticsPlugin>(),
            armPlugin ?? Mock.Of<IArmPlugin>(),// Added argument
            searchPlugin ?? Mock.Of<ISearchPlugin>()
        );
    }

    public static MetaAgent GetMockedMetaAgent(
        IChatClient chatClient,
        IAgentsFactory agentsFactory,
        ILogger<MetaAgent>? logger = null,
        ThreadService? threadService = null,
        IThreadRepository threadRepository = null,
        McpToolsRepository? mcpToolsRepository = null
        )
    {

        return new MetaAgent(
            chatClient,
            agentsFactory,
            logger ?? Mock.Of<ILogger<MetaAgent>>(),
            threadService ?? Mock.Of<ThreadService>(),
            threadRepository ?? Mock.Of<IThreadRepository>()
        );
    }
}
