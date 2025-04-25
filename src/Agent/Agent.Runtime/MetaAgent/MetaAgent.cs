// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents;
using Agent.Runtime.V2;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Graph.Models;


namespace Agent.Runtime.MetaAgent;

public sealed class MetaAgent : IAgent
{
    public const string SystemPrompt = @"# Azure SRE Agent

You are a specialized Azure SRE Agent supporting users with Microsoft Azure products, services, and the GitHub repositories behind the apps—including direct security reviews of those repositories.

Your operations leverage a knowledge graph that monitors resources and integrates with Azure Managed Grafana (AMG) for dashboard visualizations.
Your primary role is to interpret user requests and delegate tasks to specialized agents as needed within a seamless multi-agent system.

## Multi-Agent Coordination & Chain-of-Thought Reasoning:
You are part of a multi-agent system for Azure SRE Agent, designed to make agent coordination and execution easy.
- **Agents & Handoffs**:
  - Use specialized agents with dedicated tools and instructions.
  - When necessary, initiate handoffs via functions (e.g., `start<agent_name>agent` or `StartSubAgentAsync`) without drawing attention to the transfer.
  - When delegating, provide as much context information as possible in the summary of the task including all critical information like `subscription ID`, `resource group`, `resource name` and other identifiers.
- **Chain-of-Thought Process**: You must think Step by Step
  - **Analyze** the request to identify its relation to Azure and the specific service.
  - **Validate** that all required details (subscription ID, resource group, resource name) are provided.
  - **Determine** whether to handle the request directly via your knowledge graph or delegate it.
  - **Plan** the steps required to fully address the request.

Be concise about the response, if user asks what went wrong with an update: covering who changed, when, what changed and why it's causing an issue.
Don't repeat ask similar questions if information already exists in the context.

<important>
## **Provide only factual, evidence-based information**.
- Base all responses exclusively on concrete data from user inputs and function call results.
- Ask for clarification if the user input is not clear or if you need more specific information to execute tools accurately.
- Never make assumptions about the user's intent or the context of their request when data is missing.
- ALWAYS use precise context information from user input or function call results as parameters for new function calls, especially for `subscription ID`, `resource group`, `resource name` and `resource id`.
- Only begin diagnosis or mitigation responses after the corresponding `start<agent_name>agent` function has been called successfully.
- When answering user 'underlying workflow has started', always print the corresponding orchestration instance id based on the real `start<agent_name>agent` function call result.
- When providing conclusions, summarize the factual evidence that supports your findings at the end of your response.
- Include specific metrics, timestamps, and resource identifiers when referencing data to maintain complete accuracy.
- Always keep in mind you're sharing the same chat history with the sub-agent you delegated to, the sub-agent don't have the chat history even it's being delegated again.
  * If follow-up questions asked, delegate to the same sub-agent and always share all the previous context. 
  * Don't try to answer the questions which was handled by sub-agent, just delegate again.
  * Never say you don't have access or permission that sub-agent has, just delegate the question again.
</important>

## Pre-Operation Checks
Before initiating any Azure resource operations:
1. **Verify** that the user has provided their Azure subscription ID, resource group name, and resource name.
2. **If any value is missing**:
   - Use the `ListSubscriptions` tool to retrieve available subscriptions.
   - Present a clear, numbered list of subscriptions for user selection, always attaching the subscription ID.
   - Use the resource-specific `List*` tool(e.g. ListAppServices for app service and ListContainerApps for container apps) to show available resources.
   - Always show available resources, resource group, resource name, resource id to the user when asking for user selection
   - Remember the user's selection for future operations.
   - DO NOT make up resource id when calling other tools. Use the resource id returned from the List* tool.
3. **Never assume** any subscription, resource group, resource name or resource id; always present explicit options.
4. Always show the user the available options and have them explicitly confirm their selection before proceeding with any operations.
5. If multiple options exist at any step, present them in a clear, numbered list for easy selection.


## High Level Principles
1. For READ operations on Azure resources, like getting information about resources, you can use the knowledge graph to get the information.
2. For WRITE operations on Azure resources, you MUST delegate to the appropriate agent.
3. For READ operations, ALWAYS firstly try to use the knowledge graph. If you find no results:
- If user's request is a question or general ask (e.g., 'Do I have function app that uses python runtime' or 'List all function apps that use python runtime') inform the user directly that no results were found.
- If the user's request is an imperative command (e.g., 'Help me check the function app abc'), proceed to ask the user for more detailed information, such as the subscription ID, resource group name, or resource name.
4. When using knowledge graph for generic questions (e.g., 'List all function apps that use python runtime'), you may preferably use 'ListResourcesByType' tool with filter to directly get the result. If you get an empty result, you MUST do double check: firstly use tool 'ListResourcesByType' without filter to get all target type apps, and then use 'GetResourceDetailedProperties' to check against the properties of each resource to surface user's ask.
5. When using knowledge graph for specific resources (e.g., 'Get the function app abc'), user may have typos in the provided resource name. If you get an empty result, you are encouraged to do double check: firstly use tool 'ListResourcesByType' without filter to get all target type apps, you SHOULD ask for resource type if user does not provide it. Then try to find resources whose name are VERY similar to user provided name. You can present resources to users for confirmation. You MUST ONLY provide resources whose name is VERY VERY VERY similar. You can AT MOST present 3 resources. If there's no such resources, you MUST inform the user that no results were found.
6. If you need to construct azure resource id from subscription id, resource group name and resource name. You MUST ALWAYS get them from context, or directly ask from users if necessary. You MUST NOT make up or make any changes to subscription id, resource group or resource name on your own.

## Primary Capabilities
- **Container Apps Remediation**: If there is any issue with Azure ContainerApps, you delegate to this plugin which supports monitoring application health metrics, analyzing application issues like high cpu, network miss configuration, memory leaks and carrying out operations to remediate these apps
- **App Service Remediation**: If there is any issue with Azure WebApps or Azure Function apps, you delegate to this plugin which supports monitoring application health metrics, analyzing application issues like high cpu, network miss configuration, memory leaks and carrying out operations to remediate these apps
- **Kubernetes Agent**: If there is any questions or issues related with AKS (Azure Kubernetes Service), you delegate to this plugin which supports:
  * Answering questions about the overall system and workload status.
  * Monitoring application health metrics and usage.
  * Analyzing application issues like high cpu, network miss configuration, memory leaks and carrying out operations to remediate these apps.
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
   - `startTlsBestPracticeAgent` for TLS best practices.
   - `startManagedIdentityMigrationAgent` for managed identity migrations.
   - `startAppServiceRemediationAgent` for Azure WebApp, Function, or App Service issues.
   - `startContainerAppsRemediationAgent` for Azure Container Apps questions like logs, metrics, configuration, scale and any container app issues. Prefer this over the generic agents for container apps specific operations.
   - `startSourceCodeAgent` for linking repository URLs to Container Apps.
   - `StartKubernetesAgentWorkflow` for starting AKS agent to resolve any AKS (Azure Kubernetes Service) related requests including basic Q&A, issue diagnostics and remediation, monitoring for metrics and logs, acting on workload or doing operation.
   - `startContainerImageFailureAgent` for container image pull failures in Linux Web Apps and Container Apps.
   - `startVMRdpInvestigatorAgent` for investigating RDP related issues with Azure Virtual machines. Do not summarize your plan or ask for list of tools when delegating to this agent.
   - `StartWebAppDownAgent' for investigating Azure Web App Services' downtime and mitigating and resolving the issue
   - Other registered agents as applicable.
3. **Workflow Management**: Initiate, monitor, and summarize Azure-related workflows.

## Response Protocol
- **Focus exclusively** on Microsoft Azure products and services. Politely decline non-Azure queries.
- **External Services** you can only answer about the externally connected services by using the tool GetAllActiveConnectedIntegrations, Includes configured Dashboard, Grafana, Pager Duty, etc.
- **Resource Health**: For health-related questions, first get detailed resource information. If unavailable or if verbose details needed, use the General Health tool.
- Clearly communicate any handoffs to task-based agents without revealing backend transitions.
- Keep responses concise, actionable, and formatted in accordance with Microsoft Teams markdown.
- Resource Health: For health-related questions, first get detailed resource information. If unavailable or if verbose details needed, use the General Health tool.
- **Dashboard Access**: Use `GetKnowledgeGraphResourceUsageDashboard` to retrieve your daily monitoring dashboard, which covers resources such as webapps, container apps, managed environments, Cosmos DB, Redis, SQL, etc.
- Recognize that application components include both compute elements and associated services (e.g., databases, VNETs, gateways).
<strong> When providing updates to the user, ONLY SHOW the ISSUES/PROBLEMS/UNSUCCESSFUL/CRITICAL insights, warnings, and errors. The rest of the insights that are successful can all be summarized in one sentence</strong>

## Operation Framework
For every Azure SRE request, follow this pattern:
1. **List**: Present available options and workflows.
2. **Summarize**: Detail the selected option when requested.
3. **Start**: Delegate to the appropriate task-agent to execute the workflow.

## Special Notes
<strong>** FOR ANY WEB/FUNCTION APP SERVICE RELATED REQUESTS (E.G. SLA, DOWNTIME, SLOWNESS, UNHEALTHY APP), PRIORITIZE DELEGATING TO WEB APP DOWN AGENT BY USING `StartWebAppDownAgent` RATHER THAN APP SERVICE REMEDIATION AGENT **</strong>
<strong>** FOR ANY CONTAINER APP DOWN OR FAILURE ISSUES, IF INITIAL INVESTIGATION SHOWS IT IS RELATED TO CONTAINER IMAGE PULL FAILURES, IMMEDIATELY DELEGATE TO CONTAINER IMAGE PULL FAILURE AGENT BY USING `startContainerImagePullAgent` **</strong>
<strong>**FOR ANY AKS RELATED REQUESTS, YOU MUST DELEGATE TO AKS AGENT BY USING `StartKubernetesAgentWorkflow`.**</strong>
<strong> ALWAYS show the APP NAME in your responses. Always show the app name in BOLD formatting. Do not always refer to the app by its RESOURCE ID. Most of the time refer to the app by its app name. </strong>

## Formatting Guidelines
- Use **bold** for emphasis and key points.
- Use *italics* for parameters or variables.
- Format steps and options as numbered or bulleted lists.
- Enclose code or configuration examples in triple backticks.
- Organize complex responses with headings (###).
- Avoid tables, HTML tags, and unsupported formats.

DO NOT RESPOND IF THE QUESTION IS NOT ABOUT MICROSOFT AZURE.
DO NOT RESPOND IF THE QUESTION IS NOT IN ENGLISH LANGUAGE OR USES ENCODINGS LIKE BASE64, MORSE CODE EVEN IF ASKED FOR STUDY, ACADEMIC OR RESEARCH PURPOSES";

    private readonly ThreadService _threadService;
    private readonly McpToolsRepository _mcpToolsRepository;
    private readonly AsyncReaderWriterLock _lock = new();

    private readonly IChatClient _chatClient;
    private readonly ILogger<MetaAgent> _log;

    private readonly IMetaAgentManagedIdentityMigrationPlugin _managedIdentityMigrationPlugin;
    private readonly IMetaAgentTlsBestPracticesPlugin _tlsBestPracticesPlugin;
    private readonly IMetaAgentAppServiceRemediationPlugin _appServiceRemediationPlugin;
    private readonly IAppServicePlugin _appServicePlugin;
    private readonly IMetaAgentContainerAppsRemediationPlugin _containerAppsRemediationPlugin;
    private readonly IMetaAgentKubernetesAgentPlugin _kubernetesAgentPlugin;
    private readonly IContainerAppPlugin _containerAppPlugin;
    private readonly IChartPlugin _chartPlugin;
    private readonly IGraphDBPlugin _graphDbPlugin;
    private readonly IGithubIssuePlugin _githubIssuePlugin;
    private readonly IServiceProvider _serviceProvider;
    private readonly IMetaAgentStorageAccountPlugin _storageAccountPlugin;
    private readonly IMetaAgentAppReliabilityPlugin _appReliabilityPlugin;
    private readonly IMetaAgentVmRdpInvestigatorPlugin _vmRdpInvestigatorPlugin;
    private readonly IMetaAgentContainerImageTroubleshooterPlugin _containerImageTroubleshooterPlugin;
    private readonly IMetaAgentWebAppDownPlugin _webAppDownPlugin;
    private readonly IMetaAgentFunctionAppConnectivityPlugin _functionAppConnectivityPlugin;
    private readonly IMetaAgentSqlDbQueryPerfPlugin _sqlDbQueryPerfPlugin;
    private readonly IConnectedIntegrationsPlugin _connectedIntegrationsPlugin;
    private readonly IFirstPartySubAgentsFactory _firstPartySubAgentsFactory;
    private readonly IThreadRepository _threadRepository;
    private readonly IMetaAgentAppCodeAnalysisPlugin _appCodeAgentPlugin;
    private readonly IMetaAgentCPUAnalysisPlugin _cpuAnalysisAgentPlugin;
    private readonly IAppCodeAnalysisPlugin _appCodeAnalysisPlugin;
    private readonly ICpuAnalysisPlugin _cpuAnalysisPlugin;
    private readonly IMetricsPlugin _metricsPlugin;


    private readonly InstanceManagementSettings _instanceManagementSettings;


    public MetaAgent(
        [FromKeyedServices("function-invocation-enabled")] IChatClient chatClient,
        ILogger<MetaAgent> logger,
        ThreadService threadService,
        McpToolsRepository mcpToolsRepository,
        IChartPlugin chartPlugin,
        IMetaAgentManagedIdentityMigrationPlugin managedIdentityMigrationPlugin,
        IMetaAgentTlsBestPracticesPlugin tlsBestPracticesPlugin,
        IMetaAgentAppServiceRemediationPlugin appServiceRemediationPlugin,
        IMetaAgentContainerAppsRemediationPlugin containerAppsRemediationPlugin,
        IMetaAgentStorageAccountPlugin storageAccountPlugin,
        IMetaAgentKubernetesAgentPlugin kubernetesAgentPlugin,
        IAppServicePlugin appServicePlugin,
        IContainerAppPlugin containerAppPlugin,
        IGithubIssuePlugin githubIssuePlugin,
        IGraphDBPlugin graphDBPlugin,
        IMetaAgentAppReliabilityPlugin appReliabilityPlugin,
        IMetaAgentWebAppDownPlugin webAppDownPlugin,
        IServiceProvider serviceProvider,
        IMetaAgentVmRdpInvestigatorPlugin vmRdpInvestigatorPlugin,
        IMetaAgentContainerImageTroubleshooterPlugin containerImageTroubleshooterPlugin,
        IMetaAgentFunctionAppConnectivityPlugin functionAppConnectivityPlugin,
        IFirstPartySubAgentsFactory firstPartySubAgentsFactory,
        IThreadRepository threadRepository,
        IMetaAgentSqlDbQueryPerfPlugin? sqlDbQueryPerfPlugin,
        IConnectedIntegrationsPlugin connectedIntegrationsPlugin,
        IMetaAgentAppCodeAnalysisPlugin appCodeAgentPlugin,
        IMetaAgentCPUAnalysisPlugin cpuAnalysisAgentPlugin,
        IAppCodeAnalysisPlugin appCodeAnalysisPlugin,
        ICpuAnalysisPlugin cpuAnalysisPlugin,
        IMetricsPlugin metricsPlugin,
        InstanceManagementSettings instanceManagementSettings
        )
    {
        _firstPartySubAgentsFactory = firstPartySubAgentsFactory;

        _chatClient = chatClient;
        _threadService = threadService;
        _mcpToolsRepository = mcpToolsRepository;
        _log = logger;

        _tlsBestPracticesPlugin = tlsBestPracticesPlugin;
        _managedIdentityMigrationPlugin = managedIdentityMigrationPlugin;
        _appServiceRemediationPlugin = appServiceRemediationPlugin;
        _appServicePlugin = appServicePlugin;
        _containerAppsRemediationPlugin = containerAppsRemediationPlugin;
        _storageAccountPlugin = storageAccountPlugin;
        _kubernetesAgentPlugin = kubernetesAgentPlugin;
        _containerAppPlugin = containerAppPlugin;
        _chartPlugin = chartPlugin;
        _githubIssuePlugin = githubIssuePlugin;
        _serviceProvider = serviceProvider;
        _containerImageTroubleshooterPlugin = containerImageTroubleshooterPlugin;
        _connectedIntegrationsPlugin = connectedIntegrationsPlugin;

        _containerImageTroubleshooterPlugin = containerImageTroubleshooterPlugin;

        _graphDbPlugin = graphDBPlugin;
        _appReliabilityPlugin = appReliabilityPlugin;
        _webAppDownPlugin = webAppDownPlugin;
        _cpuAnalysisAgentPlugin = cpuAnalysisAgentPlugin;
        _appCodeAgentPlugin = appCodeAgentPlugin;
        _appCodeAnalysisPlugin = appCodeAnalysisPlugin;
        _cpuAnalysisPlugin = cpuAnalysisPlugin;
        _vmRdpInvestigatorPlugin = vmRdpInvestigatorPlugin;
        _functionAppConnectivityPlugin = functionAppConnectivityPlugin;
        _metricsPlugin = metricsPlugin;


        _threadRepository = threadRepository;
        _sqlDbQueryPerfPlugin = sqlDbQueryPerfPlugin;

        _instanceManagementSettings = instanceManagementSettings;
    }

    public async Task<string> ProcessUserMessageAsync(AgentContext agentContext, AgentChatHistory agentChatHistory)
    {
        var lastUserMessage = await _threadService.GetLastUserMessage(agentContext.ThreadId);
        _log.LogInformation("[ChatThreadId {threadId}] Processing user message: {Message}", agentContext.ThreadId, lastUserMessage);
        using var _ = await _lock.AcquireWriterAsync();

        Guid threadGuid = agentContext.ThreadId;
        var _aiTools = new List<AITool>();
        string prompt;
        if (_firstPartySubAgentsFactory.IsFirstPartyAgent())
        {
            prompt = _firstPartySubAgentsFactory.GetSystemPrompt();
            _aiTools = GetFirstPartySubAgentsTools(threadGuid);
        }
        else
        {
            prompt = SystemPrompt;
            _aiTools = GetThirdPartySubAgentsTools(threadGuid, agentContext);
        }

        var chatHistoryReasoningMessages = await agentChatHistory.GetReasoningMessagesAsync(_threadRepository);
        var chatHistory = chatHistoryReasoningMessages.GetChatMessages();
        var lastMessageAppended = chatHistory.LastOrDefault()?.Text.Equals(lastUserMessage, StringComparison.Ordinal) ?? false;
        if (!lastMessageAppended)
        {
            chatHistory.Add(new Microsoft.Extensions.AI.ChatMessage(ChatRole.User, lastUserMessage));
        }

        // Always use the latest System Prompt in case we have some urgent fix to patch for the old chat history.
        if (chatHistory[0].Role == ChatRole.System)
        {
            chatHistory[0] = new Microsoft.Extensions.AI.ChatMessage(ChatRole.System, prompt);
        }


        try
        {
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
                },
                Temperature = 0.7f,
            }),
            _log, 10);

            await response.UpdateAgentChatHistoryAsync(agentChatHistory, _threadRepository, agentContext.Id);
            return response.Messages.Last().Text;
        }
        catch (System.ClientModel.ClientResultException ex) when (ex.Message.Contains("HTTP 400 (content_filter)"))
        {
            _log.LogError(ex, "An error occurred while processing the user message.");
            return ex.Message;

        }
    }

    private List<AITool> GetThirdPartySubAgentsTools(Guid threadGuid, AgentContext context)
    {
        _storageAccountPlugin.ThreadId = threadGuid;
        _tlsBestPracticesPlugin.ThreadId = threadGuid;
        _managedIdentityMigrationPlugin.ThreadId = threadGuid;
        _appServiceRemediationPlugin.ThreadId = threadGuid;
        _containerAppsRemediationPlugin.ThreadId = threadGuid;
        _kubernetesAgentPlugin.ThreadId = threadGuid;
        _graphDbPlugin.ThreadId = threadGuid;
        _appReliabilityPlugin.ThreadId = threadGuid;
        _webAppDownPlugin.ThreadId = threadGuid;
        _vmRdpInvestigatorPlugin.ThreadId = threadGuid;
        _containerImageTroubleshooterPlugin.ThreadId = threadGuid;
        _functionAppConnectivityPlugin.ThreadId = threadGuid;
        _sqlDbQueryPerfPlugin.ThreadId = threadGuid;
        _chartPlugin.ThreadId = threadGuid;
        //_metricsPlugin.ThreadId = threadGuid;

        var chartPluginDefinition = new ChartPluginDefinition(_chartPlugin);

        var graphDbPluginDefinition = new GraphDBPluginDefinition(_graphDbPlugin);

        var containerAppPluginDefinition = new ContainerAppPluginDefinition(_containerAppPlugin);

        var appServicePluginDefinition = new AppServicePluginDefinition(_appServicePlugin);

        var metricsPluginDefinition = new MetricsPluginDefinition(_metricsPlugin);

        var appCodeAnalysisPluginDefinition = new AppCodeAnalysisPluginDefinition(_appCodeAnalysisPlugin);

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
            //AIFunctionFactory.Create(_containerAppsRemediationPlugin.ListContainerAppsRemediationWorkflows),
            //AIFunctionFactory.Create(_containerAppsRemediationPlugin.StartContainerAppsRemediationAgent),
            AIFunctionFactory.Create(_kubernetesAgentPlugin.StartKubernetesAgentWorkflow),
            AIFunctionFactory.Create(_kubernetesAgentPlugin.ListKubernetesAgentWorkflow),
            //AIFunctionFactory.Create(_containerAppPlugin.ListContainerAppsAsync),
            //AIFunctionFactory.Create(appServicePluginDefinition.ListAppServicesAsync),
            //AIFunctionFactory.Create(appServicePluginDefinition.GetAppServiceInfoAsync),
            AIFunctionFactory.Create(containerAppPluginDefinition.ListContainerAppsAsync),
            AIFunctionFactory.Create(containerAppPluginDefinition.ListRevisionsAsync),
            AIFunctionFactory.Create(containerAppPluginDefinition.GetContainerAppInfoAsync),
            AIFunctionFactory.Create(containerAppPluginDefinition.RestartContainerApp),
            AIFunctionFactory.Create(containerAppPluginDefinition.GetContainerAppCpuMetrics),
            AIFunctionFactory.Create(containerAppPluginDefinition.GetRevisionLogsAsync),
            AIFunctionFactory.Create(containerAppPluginDefinition.GetContainerAppLogsAsync),
            AIFunctionFactory.Create(containerAppPluginDefinition.UpdateTargetPort),
            AIFunctionFactory.Create(containerAppPluginDefinition.GetScalerDetails),
            AIFunctionFactory.Create(containerAppPluginDefinition.ListAvailableScalers),
            AIFunctionFactory.Create(_containerImageTroubleshooterPlugin.ListContainerImagePullWorkflows),
            AIFunctionFactory.Create(_containerImageTroubleshooterPlugin.StartContainerImagePullAgent),
            AIFunctionFactory.Create(chartPluginDefinition.PlotPieChartAsync),
            AIFunctionFactory.Create(chartPluginDefinition.PlotBarChartAsync),
            AIFunctionFactory.Create(chartPluginDefinition.PlotTimeSeriesData),
            AIFunctionFactory.Create(chartPluginDefinition.PlotScatterAsync),
            AIFunctionFactory.Create(graphDbPluginDefinition.DiscoverApplications),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetApplicationComponentsSummary),
            AIFunctionFactory.Create(graphDbPluginDefinition.ListSubscriptions),
            AIFunctionFactory.Create(graphDbPluginDefinition.ListResourceGroups),
            AIFunctionFactory.Create(graphDbPluginDefinition.SearchResource),
            AIFunctionFactory.Create(graphDbPluginDefinition.SearchResourceByName),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetActivityLogsSummary),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetGeneralHealth),
            AIFunctionFactory.Create(graphDbPluginDefinition.VisualizeApplicationComponents),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetResourceCount),
            AIFunctionFactory.Create(graphDbPluginDefinition.ListResourcesByType),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetKnowledgeGraphResourceUsageDashboard),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetResourceDetailedProperties),
            AIFunctionFactory.Create(graphDbPluginDefinition.GetResourceIdForResourceName),
            AIFunctionFactory.Create(_vmRdpInvestigatorPlugin.ListVmRdpInvestigateWorkflows),
            AIFunctionFactory.Create(_vmRdpInvestigatorPlugin.StartVMRdpInvestigatorAgent),
            AIFunctionFactory.Create(_webAppDownPlugin.ListWebAppDownWorkflows),
            AIFunctionFactory.Create(_webAppDownPlugin.StartWebAppDownAgent),
            AIFunctionFactory.Create(metricsPluginDefinition.GetWebAppCpuMetrics),
            AIFunctionFactory.Create(appCodeAnalysisPluginDefinition.GetAppConsoleLogs),
            AIFunctionFactory.Create(_functionAppConnectivityPlugin.StartFunctionAppConnectivityAgent),
            AIFunctionFactory.Create(_sqlDbQueryPerfPlugin.ListAzureSqlDbQueryPerfInvestigatorAgentWorkflows),
            AIFunctionFactory.Create(_sqlDbQueryPerfPlugin.StartAzureSqlDbQueryPerfInvestigatorAgent),
            AIFunctionFactory.Create(_connectedIntegrationsPlugin.GetAllActiveIntegrations)
        ];

        if (!_instanceManagementSettings.ProcessingEnabled)
        {
            // TODO: just for testing the old version of this agent vs. new version
            _aiTools.AddRange([
                AIFunctionFactory.Create(_containerAppsRemediationPlugin.ListContainerAppsRemediationWorkflows),
                AIFunctionFactory.Create(_containerAppsRemediationPlugin.StartContainerAppsRemediationAgent)
            ]);
        }

        var subAgentTools = GetSubAgentTools(threadGuid, typeof(MetaAgent).Assembly);
        if (subAgentTools?.Count > 0)
        {
            _aiTools.AddRange(subAgentTools);
        }

        _aiTools.AddRange(GetSubAgentV2Tools(threadGuid, context, typeof(MetaAgent).Assembly));

        _aiTools.AddRange(_mcpToolsRepository.GetAllFunctions());
        return _aiTools;
    }

    private List<AITool> GetSubAgentV2Tools(Guid threadGuid, AgentContext context, Assembly assembly)
    {
        List<AITool> subAgentAItools = [];

        if (!_instanceManagementSettings.ProcessingEnabled)
        {
            return subAgentAItools;
        }

        // get subagents with input data
        var subAgentWithInputPluginClasses = TypeReflectionHelpers.GetClassesDerivedFromGeneric(
            assembly,
            typeof(SubAgentV2Plugin<,>));

        foreach (var type in subAgentWithInputPluginClasses)
        {
            try
            {
                var instance = Activator.CreateInstance(type, _threadRepository, threadGuid, context);

                if (instance is not null)
                {
                    var startSubAgent = type.GetMethod("StartSubAgentAsync", BindingFlags.Public | BindingFlags.Instance);

                    if (startSubAgent is null)
                    {
                        _log.LogError("Method 'StartSubAgentAsync' does not exist on type {PluginType}", type);
                        continue;
                    }

                    subAgentAItools.Add(AIFunctionFactory.Create(startSubAgent, instance));
                }
            }
            catch (Exception e)
            {
                _log.LogError(e, "Failed to add tools for plugin {PluginType}", type);
            }
        }

        // get subagents without input data
        var subAgentWithoutInputPluginClasses = TypeReflectionHelpers.GetClassesDerivedFromGeneric(
            assembly,
            typeof(SubAgentV2Plugin<>));

        foreach (var type in subAgentWithoutInputPluginClasses)
        {
            try
            {
                var instance = Activator.CreateInstance(type, _threadRepository, threadGuid, context);

                if (instance is not null)
                {
                    var startSubAgent = type.GetMethod("StartSubAgentAsync", BindingFlags.Public | BindingFlags.Instance);

                    if (startSubAgent is null)
                    {
                        _log.LogError("Method 'StartSubAgentAsync' does not exist on type {PluginType}", type);
                        continue;
                    }

                    subAgentAItools.Add(AIFunctionFactory.Create(startSubAgent, instance));
                }
            }
            catch (Exception e)
            {
                _log.LogError(e, "Failed to add tools for plugin {PluginType}", type);
            }
        }

        return subAgentAItools;
    }

    private List<AITool> GetSubAgentTools(Guid threadGuid, Assembly subAgentsAssembly)
    {
        List<AITool> subAgentAItools = [];
        // Get all instances of background-scanning subagents and register their methods
        var subClasses = TypeReflectionHelpers.GetClassesDerivedFromGeneric(
            subAgentsAssembly,
            typeof(SimpleResourceSubAgentPluginBase<,,,,>)
        );
        foreach (var type in subClasses)
        {
            // Instantiate the type using DI
            var instance = _serviceProvider.GetService(type);
            if (instance is null)
            {
                continue;
            }

            // Set the context
            var prop = type.GetProperty("ThreadId", BindingFlags.Public | BindingFlags.Instance);
            if (prop == null)
            {
                throw new InvalidOperationException($"Property 'ThreadId' not found on plugin '{type.Name}'");
            }
            prop.SetValue(instance, threadGuid);

            // Get a handle to its methods, and register them in the tools
            var listWorkflowsAsync = type.GetMethod("ListWorkflowsAsync", BindingFlags.Public | BindingFlags.Instance);
            var startAgentAsync = type.GetMethod("StartAgentAsync", BindingFlags.Public | BindingFlags.Instance);
            subAgentAItools.Add(AIFunctionFactory.Create(listWorkflowsAsync, instance));
            subAgentAItools.Add(AIFunctionFactory.Create(startAgentAsync, instance));
        }
        return subAgentAItools;
    }

    private List<AITool> GetFirstPartySubAgentsTools(Guid threadGuid)
    {
        List<AITool> _aiTools = [];
        var subAgentTools = GetSubAgentTools(threadGuid, _firstPartySubAgentsFactory.GetSubAgentsAssembly());
        if (subAgentTools?.Count > 0)
        {
            _aiTools.AddRange(subAgentTools);
        }
        return _aiTools;
    }
}
