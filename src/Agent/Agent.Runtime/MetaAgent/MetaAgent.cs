// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.MetaAgent;

public sealed class MetaAgent : IAgent
{
    private const string SystemPrompt = @"# Azure SRE Agent

You are a specialized Azure SRE Agent supporting users with Microsoft Azure products, services, and the GitHub repositories behind the apps—including direct security reviews of those repositories.

Your operations leverage a knowledge graph that monitors resources and integrates with Azure Managed Grafana (AMG) for dashboard visualizations. Your primary role is to interpret user requests and delegate tasks to specialized agents as needed within a seamless multi-agent system.

## Multi-Agent Coordination & Chain-of-Thought Reasoning:
You are part of a multi-agent system for Azure SRE Agent, designed to make agent coordination and execution easy.
- **Agents & Handoffs**: Use specialized agents with dedicated tools and instructions. When necessary, initiate handoffs via functions (e.g., `start<agent_name>agent`) without drawing attention to the transfer.
- **Chain-of-Thought Process**: You must think Step by Step
  - **Analyze** the request to identify its relation to Azure and the specific service.
  - **Validate** that all required details (subscription ID, resource group, resource name) are provided.
  - **Determine** whether to handle the request directly via your knowledge graph or delegate it.
  - **Plan** the steps required to fully address the request.

Be concise about the response, if user asks what went wrong with an update: covering who changed, when, what changed and why it's causing an issue.

## Pre-Operation Checks
Before initiating any Azure resource operations:
1. **Verify** that the user has provided their Azure subscription ID, resource group name, and resource name.
2. **If any value is missing**:
   - Use the `List.Subscriptions` tool to retrieve available subscriptions.
   - Present a clear, numbered list of subscriptions for user selection.
   - Use the resource-specific `List` tool to show available resources.
   - Confirm the exact resource name with the user.
3. **Never assume** any subscription, resource group, or resource name; always present explicit options.

2. If ANY of these values are missing, you must:
   - First use the List.Subscriptions tool to retrieve available subscriptions
   - Present the subscriptions to the user and ask them to select one
   - Then use individual resource specific List tool to List the resources
   - Have the user confirm the specific resource name

3. Never assume any subscription ID, resource group name, or resource name values.
   
4. Always show the user the available options and have them explicitly confirm their selection before proceeding with any operations.

5. If multiple options exist at any step, present them in a clear, numbered list for easy selection.

**You must not assume any of these values**
</Important>

## Primary Capabilities
- **Container Apps Remediation**: If there is any issue with Azure ContainerApps, you delegate to this plugin which supports monitoring application health metrics, analyzing application issues like high cpu, network miss configuration, memory leaks and carrying out operations to remediate these apps
- **App Service Remediation**: If there is any issue with Azure WebApps or Azure Function apps, you delegate to this plugin which supports monitoring application health metrics, analyzing application issues like high cpu, network miss configuration, memory leaks and carrying out operations to remediate these apps
- **Kubernetes Agent**: If there is any issue with Azure Kubernetes Service, you delegate to this plugin which supports monitoring application health metrics, analyzing application issues like high cpu, network miss configuration, memory leaks and carrying out operations to remediate these apps
- **Managed Identity Migration**: Help users migrate from certificate-based authentication to managed identities
- **TLS Best Practices**: Guide users in implementing TLS best practices for Azure resources
- **Source Code Scanning**: Help users link repo urls to their Azure Container Apps
- **Storage Account Remediation**: Help users with making changes storage account settings
- **App Reliability**: Delegate to this plugin to help users improve the reliability of their Azure applications
- **VM Rdp Investigator**: Help users investigate issues related to RDP to a Virual Machine
- **Container Image Pull Failure Investigation**: Help users diagnose and fix container image pull failures in Linux Web Apps and Container Apps
- **Web App Down**: Help users mitigate and resolve any issues with Web App Services being slow or having downtime.
- **Function App Connectivity Troubleshooting**: Help users test connectivity from their Function app to Storage account

## Core Responsibilities
1. **Request Triage**: Confirm that the user query pertains to Azure SRE matters.
2. **Task Delegation**: Route requests to specialized task-agents such as:
   - `startTlsBestPracticeAgent` for TLS best practices or webapp migration queries.
   - `startManagedIdentityMigrationAgent` for managed identity migrations.
   - `startAppServiceRemediationAgent` for Azure WebApp, Function, or App Service issues.
   - `startContainerAppsRemediationAgent` for Azure Container Apps concerns.
   - `startSourceCodeAgent` for linking repository URLs to Container Apps.
   - `startKubernetesAgent` for any AKS (Kubernetes) related requests including issue diagnostics and remediation, monitoring for metrics and logs, acting on workload or doing operation.
   - `startContainerImageFailureAgent` for container image pull failures in Linux Web Apps and Container Apps.
   - `startVMRdpInvestigatorAgent` for investigating RDP related issues with Azure Virtual machines. Do not summairze your plan or ask for list of tools when delegating to this agent.
   - Other registered agents as applicable.
3. **Workflow Management**: Initiate, monitor, and summarize Azure-related workflows.

## Response Protocol
- **Focus exclusively** on Microsoft Azure products and services. Politely decline non-Azure queries.
- Clearly communicate any handoffs to task-based agents without revealing backend transitions.
- Keep responses concise, actionable, and formatted in accordance with Microsoft Teams markdown.
- **Dashboard Access**: Use `GetKnowledgeGraphResourceUsageDashboard` to retrieve your daily monitoring dashboard, which covers resources such as webapps, container apps, managed environments, Cosmos DB, Redis, SQL, etc.
- Recognize that application components include both compute elements and associated services (e.g., databases, VNETs, gateways).

## Operation Framework
For every Azure SRE request, follow this pattern:
1. **List**: Present available options and workflows.
2. **Summarize**: Detail the selected option when requested.
3. **Start**: Delegate to the appropriate task-agent to execute the workflow.

## Formatting Guidelines
- Use **bold** for emphasis and key points.
- Use *italics* for parameters or variables.
- Format steps and options as numbered or bulleted lists.
- Enclose code or configuration examples in triple backticks.
- Organize complex responses with headings (###).
- Avoid tables, HTML tags, and unsupported formats.

DO NOT RESPOND IF THE QUESTION IS NOT ABOUT MICROSOFT AZURE.";

    private readonly ThreadService _threadService;
    private readonly McpToolsRepository _mcpToolsRepository;
    private readonly AsyncReaderWriterLock _lock = new();

    private readonly IChatClient _chatClient;
    private readonly ILogger<MetaAgent> _log;

    private readonly ManagedIdentityMigrationPlugin _managedIdentityMigrationPlugin;
    private readonly TlsBestPracticesPlugin _tlsBestPracticesPlugin;
    private readonly AppServiceRemediationPlugin _appServiceRemediationPlugin;
    private readonly IAppServicePlugin _appServicePlugin;
    private readonly ContainerAppsRemediationPlugin _containerAppsRemediationPlugin;
    private readonly KubernetesAgentPlugin _kubernetesAgentPlugin;
    private readonly IContainerAppPlugin _containerAppPlugin;
    private readonly ChartPlugin _chartplugin;
    private readonly IGraphDBPlugin _graphDbPlugin;
    private readonly IGithubIssuePlugin _githubIssuePlugin;
    private readonly IServiceProvider _serviceProvider;
    private readonly StorageAccountPlugin _storageAccountPlugin;
    private readonly AppReliabilityPlugin _appReliabilityPlugin;
    private readonly VmRdpInvestigatorPlugin _vmRdpInvestigatorPlugin;
    private readonly ContainerImageTroubleshooterPlugin _containerImageTroubleshooterPlugin;
    private readonly WebAppDownPlugin _webAppDownPlugin;
    private readonly FunctionAppConnectivityPlugin _functionAppConnectivityPlugin;
    private readonly DashboardSettings _dashboardSettings;

    public MetaAgent(
        [FromKeyedServices("function-invocation-enabled")] IChatClient chatClient,
        ILogger<MetaAgent> logger,
        ThreadService threadService,
        McpToolsRepository mcpToolsRepository,
        Plugins.ChartPlugin chartplugin,
        DashboardSettings dashboardSettings,
        ManagedIdentityMigrationPlugin managedIdentityMigrationPlugin,
        TlsBestPracticesPlugin tlsBestPracticesPlugin,
        AppServiceRemediationPlugin appServiceRemediationPlugin,
        ContainerAppsRemediationPlugin containerAppsRemediationPlugin,
        StorageAccountPlugin storageAccountPlugin,
        KubernetesAgentPlugin kubernetesAgentPlugin,
        IAppServicePlugin appServicePlugin,
        IContainerAppPlugin containerAppPlugin,
        IGithubIssuePlugin githubIssuePlugin,
        IGraphDBPlugin graphDBPlugin,
        AppReliabilityPlugin appReliabilityPlugin,
        WebAppDownPlugin webAppDownPlugin,
        IServiceProvider serviceProvider,
        VmRdpInvestigatorPlugin vmRdpInvestigatorPlugin,
        ContainerImageTroubleshooterPlugin containerImageTroubleshooterPlugin,
        FunctionAppConnectivityPlugin functionAppConnectivityPlugin
        )
    {
        _chatClient = chatClient;
        _threadService = threadService;
        _mcpToolsRepository = mcpToolsRepository;
        _log = logger;
        _dashboardSettings = dashboardSettings;

        _tlsBestPracticesPlugin = tlsBestPracticesPlugin;
        _managedIdentityMigrationPlugin = managedIdentityMigrationPlugin;
        _appServiceRemediationPlugin = appServiceRemediationPlugin;
        _appServicePlugin = appServicePlugin;
        _containerAppsRemediationPlugin = containerAppsRemediationPlugin;
        _storageAccountPlugin = storageAccountPlugin;
        _kubernetesAgentPlugin = kubernetesAgentPlugin;
        _containerAppPlugin = containerAppPlugin;
        _chartplugin = chartplugin;
        _githubIssuePlugin = githubIssuePlugin;
        _serviceProvider = serviceProvider;
        _containerImageTroubleshooterPlugin = containerImageTroubleshooterPlugin;

        _containerImageTroubleshooterPlugin = containerImageTroubleshooterPlugin;

        _graphDbPlugin = graphDBPlugin;
        _appReliabilityPlugin = appReliabilityPlugin;
        _webAppDownPlugin = webAppDownPlugin;
        _vmRdpInvestigatorPlugin = vmRdpInvestigatorPlugin;
        _functionAppConnectivityPlugin = functionAppConnectivityPlugin;
    }

    public async Task<string> ProcessUserMessage(ThreadContext ctx)
    {
        var lastUserMessage = await _threadService.GetLastUserMessage(ctx);
        _log.LogInformation("[ChatThreadId {threadId}] Processing user message: {Message}", ctx.ThreadId, lastUserMessage);
        using var _ = await _lock.AcquireWriterAsync();

        Guid threadGuid = ctx.ThreadId;

        _storageAccountPlugin.Context = ctx;
        _tlsBestPracticesPlugin.Context = ctx;
        _managedIdentityMigrationPlugin.Context = ctx;
        _appServiceRemediationPlugin.Context = ctx;
        _containerAppsRemediationPlugin.Context = ctx;
        _kubernetesAgentPlugin.Context = ctx;
        _graphDbPlugin.Context = ctx;
        _appReliabilityPlugin.Context = ctx;
        _webAppDownPlugin.Context = ctx;
        _vmRdpInvestigatorPlugin.Context = ctx;
        _containerImageTroubleshooterPlugin.Context = ctx;
        _functionAppConnectivityPlugin.Context = ctx;

        var chartPluginDefinition = new ChartPluginDefinition(_chartplugin);
        _chartplugin.Context = ctx;

        var graphDbPluginDefinition = new GraphDBPluginDefinition(_graphDbPlugin);

        var containerAppPluginDefinition = new ContainerAppPluginDefinition(_containerAppPlugin);

        var appServicePluginDefinition = new AppServicePluginDefinition(_appServicePlugin);

        List<AITool> _aiTools =
        [
            AIFunctionFactory.Create(_managedIdentityMigrationPlugin.ListManagedIdentityMigrations),
            AIFunctionFactory.Create(_managedIdentityMigrationPlugin.StartManagedIdentityMigrationAgent),
            AIFunctionFactory.Create(_tlsBestPracticesPlugin.ListTlsBestPracticeWorkflows),
            AIFunctionFactory.Create(_tlsBestPracticesPlugin.StartTlsBestPracticeAgent),
            AIFunctionFactory.Create(_appReliabilityPlugin.ListAppReliabilityWorkflows),
            AIFunctionFactory.Create(_appReliabilityPlugin.StartAppReliabilityAgent),
            AIFunctionFactory.Create(_appServiceRemediationPlugin.ListAppServiceRemediationWorkflows),
            AIFunctionFactory.Create(_appServiceRemediationPlugin.StartAppServiceRemediationAgent),
            AIFunctionFactory.Create(_containerAppsRemediationPlugin.ListContainerAppsRemediationWorkflows),
            AIFunctionFactory.Create(_containerAppsRemediationPlugin.StartContainerAppsRemediationAgent),
            AIFunctionFactory.Create(_kubernetesAgentPlugin.StartKubernetesAgentWorkflow),
            AIFunctionFactory.Create(_kubernetesAgentPlugin.ListKubernetesAgentWorkflow),
            AIFunctionFactory.Create(_containerAppPlugin.ListContainerAppsAsync),
            AIFunctionFactory.Create(appServicePluginDefinition.ListAppServicesAsync),
            AIFunctionFactory.Create(appServicePluginDefinition.GetAppServiceInfoAsync),
            AIFunctionFactory.Create(containerAppPluginDefinition.ListContainerAppsAsync),
            AIFunctionFactory.Create(containerAppPluginDefinition.GetContainerAppInfoAsync),
            AIFunctionFactory.Create(_containerImageTroubleshooterPlugin.ListContainerImagePullWorkflows),
            AIFunctionFactory.Create(_containerImageTroubleshooterPlugin.StartContainerImagePullAgent),
            AIFunctionFactory.Create(chartPluginDefinition.PlotPieChartAsync),
            AIFunctionFactory.Create(chartPluginDefinition.PlotBarChartAsync),
            AIFunctionFactory.Create(chartPluginDefinition.PlotTimeSeriesDataAsync),
            AIFunctionFactory.Create(graphDbPluginDefinition.DiscoverApplications),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetApplicationComponentsSummary),
            AIFunctionFactory.Create(graphDbPluginDefinition.ListSubscriptions),
            AIFunctionFactory.Create(graphDbPluginDefinition.SearchResource),
            AIFunctionFactory.Create(graphDbPluginDefinition.SearchResourceByName),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetActivityLogsSummary),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetGeneralHealth),
            AIFunctionFactory.Create(graphDbPluginDefinition.VisualizeApplicationComponents),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetResourceCount),
            AIFunctionFactory.Create(graphDbPluginDefinition.ListResourcesByType),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetKnowledgeGraphResourceUsageDashboard),
            AIFunctionFactory.Create(graphDbPluginDefinition.VisualizeAKSMicroserviceTopology),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetResourceDetailedProperties),
            AIFunctionFactory.Create(_vmRdpInvestigatorPlugin.ListVmRdpInvestigateWorkflows),
            AIFunctionFactory.Create(_vmRdpInvestigatorPlugin.StartVMRdpInvestigatorAgent),
            AIFunctionFactory.Create(_webAppDownPlugin.ListWebAppDownWorkflows),
            AIFunctionFactory.Create(_webAppDownPlugin.StartWebAppDownAgent),
            AIFunctionFactory.Create(_functionAppConnectivityPlugin.StartFunctionAppConnectivityAgent)
        ];

        // Get all instances of background-scanning subagents and register their methods
        var subClasses = TypeReflectionHelpers.GetClassesDerivedFromGeneric(
            typeof(MetaAgent).Assembly,
            typeof(SimpleResourceSubAgentPluginBase<,,,,>)
        );
        foreach (var type in subClasses)
        {
            // Instantiate the type using DI
            var instance = _serviceProvider.GetRequiredService(type);

            // Set the context
            var prop = type.GetProperty("Context", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                throw new InvalidOperationException($"Property 'Context' not found on plugin '{type.Name}'");
            }
            prop.SetValue(instance, ctx);

            // Get a handle to its methods, and register them in the tools
            var listWorkflowsAsync = type.GetMethod("ListWorkflowsAsync", BindingFlags.Public | BindingFlags.Instance);
            var startAgentAsync = type.GetMethod("StartAgentAsync", BindingFlags.Public | BindingFlags.Instance);
            _aiTools.Add(AIFunctionFactory.Create(listWorkflowsAsync, instance));
            _aiTools.Add(AIFunctionFactory.Create(startAgentAsync, instance));
        }


        _aiTools.AddRange(_mcpToolsRepository.GetAllFunctions());
        var chatHistory = await _threadService.ToLLMChatHistory(ctx, SystemPrompt);

        var response = await ChatClientHelper.ExecuteWithRetryAsync(
            async () => await _chatClient.GetResponseAsync(
                chatHistory,
                new ChatOptions
                {
                    Tools = _aiTools,
                    ToolMode = ChatToolMode.Auto,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        //["AllowParallelToolCalls"] = false,
                    }
                }),
            _log, 10);

        return response.Messages.Last().Text;
    }
}
