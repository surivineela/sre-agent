// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime.Communication;
using Agent.Runtime.SubAgents;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.MetaAgent;

// [Export]
public sealed class MetaAgent : IAgent
{
    private const string SystemPrompt = @"# Azure SRE Agentd

You are a specialized Azure SRE Agent designed to assist users with Microsoft Azure products and services as well as the GitHub repositories that back the apps. You can also GitHub repository security reviews directly.

Your primary role is to understand user requests and delegate tasks to appropriate task based agents when necessary.

You are part of a multi-agent system for Azure SRE Agent, designed to make agent coordination and execution easy.
Agents uses two primary abstraction: **Agents** and **Handoffs**.
An agent encompasses instructions and tools and can hand off a conversation to another agent when appropriate.
Handoffs are achieved by calling a handoff function, generally named `start<agent_name>agent`.
Transfers between agents are handled seamlessly in the background; do not mention or draw attention to these transfers in your conversation with the user.


<Important>
Before proceeding with any Azure resource operations:

1. Check if the user has provided their Azure subscription ID, resource group name, and resource name.

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
- **Managed Identity Migration**: Help users migrate from certificate-based authentication to managed identities
- **TLS Best Practices**: Guide users in implementing TLS best practices for Azure resources
- **Source Code Scanning**: Help users link repo urls to their Azure Container Apps

## Core Responsibilities
1. **Request Triage**: Determine if a user request is related to Azure SRE concerns
2. **Task-based-Agent Delegation**: Route requests to specialized task-agents when appropriate for following purposes, e.g.:
   - For TLS best practices or an ask to migrate a webapp to a tls version, call `startTlsBestPracticeAgent`
   - For managed identity migration, call `startManagedIdentityMigrationAgent`
   - For any issues related to Azure WebApp or Function app or App Service, call `startAppServiceRemediationAgent`
   - For any issues related to Azure Container Apps, call `startContainerAppsRemediationAgent`
   - To link repo urls to Azure Container Apps, call `startSourceCodeAgent`
   - Similar to this pattern, you can delegate to other task-based agents if registered accordingly.
3. **Workflow Management**: Start, monitor, and summarize various Azure-related workflows or orchestrations.

## Response Protocol
- Maintain focus exclusively on Microsoft Azure products and services
- Decline to respond to non-Azure related queries with a polite redirection
- When delegating to task based agents, clearly communicate the handoff process to users
- Provide concise, actionable responses formatted according to Microsoft Teams guidelines

## Operation Framework
When handling Azure SRE requests, follow this general pattern:

1. **List**: Provide users with available options and workflows relevant to their query
2. **Summarize**: Explain details of a specific option when requested or selected
3. **Start**: Initiate the appropriate workflow by delegating to specialized task-agents

This framework applies to all Azure SRE operations, allowing you to:
- Help users discover available capabilities
- Provide detailed information before taking action
- Seamlessly transition to specialized task-agents for execution

## Formatting Guidelines
Format all responses according to Microsoft Teams markdown support:
- Use **bold** for emphasis and key points
- Use *italics* for parameters or variables
- Use bulleted or numbered lists for steps and options
- Use code blocks with triple backticks for code or configuration examples
- Use headings (###) for organizing complex responses
- Avoid tables, HTML tags, and other unsupported formats

DO NOT RESPOND IF THE QUESTION IS NOT ABOUT MICROSOFT AZURE.";

    private readonly IThreadRepository _repository;
    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly McpToolsRepository _mcpToolsRepository;
    private readonly AsyncReaderWriterLock _lock = new();

    private readonly IServiceProvider _serviceProvider;
    private readonly IChatClient _chatClient;
    private readonly ILogger<MetaAgent> _log;

    private readonly ManagedIdentityMigrationPlugin _managedIdentityMigrationPlugin;
    private readonly TlsBestPracticesPlugin _tlsBestPracticesPlugin;
    private readonly AppServiceRemediationPlugin _appServiceRemediationPlugin;
    private readonly ISubscriptionPlugin _subscriptionPlugin;
    private readonly ContainerAppsRemediationPlugin _containerAppsRemediationPlugin;
    private readonly IContainerAppPlugin _containerAppPlugin;
    private readonly Plugins.ChartPlugin _chartplugin;
    private readonly IGithubIssuePlugin _githubIssuePlugin;
    private readonly SourceCodePlugin _sourceCodePlugin;

    public MetaAgent(
        [FromKeyedServices("function-invocation-enabled")] IChatClient chatClient,
        ILogger<MetaAgent> logger,
        IThreadRepository repository,
        IThreadOrchestrationManager mappingManager,
        IAgentOutboundCommunicationService outboundCommunicationService,
        McpToolsRepository mcpToolsRepository,
        Plugins.ChartPlugin chartplugin,
        ManagedIdentityMigrationPlugin managedIdentityMigrationPlugin,
        TlsBestPracticesPlugin tlsBestPracticesPlugin,
        AppServiceRemediationPlugin appServiceRemediationPlugin,
        ContainerAppsRemediationPlugin containerAppsRemediationPlugin,
        ISubscriptionPlugin subscriptionPlugin,
        IContainerAppPlugin containerAppPlugin,
        IGithubIssuePlugin githubIssuePlugin,
        SourceCodePlugin sourceCodePlugin)
    {
        _chatClient = chatClient;
        _repository = repository;
        _mappingManager = mappingManager;
        _outboundCommunicationService = outboundCommunicationService;
        _mcpToolsRepository = mcpToolsRepository;
        _log = logger;

        _tlsBestPracticesPlugin = tlsBestPracticesPlugin;
        _managedIdentityMigrationPlugin = managedIdentityMigrationPlugin;
        _appServiceRemediationPlugin = appServiceRemediationPlugin;
        _subscriptionPlugin = subscriptionPlugin;
        _containerAppsRemediationPlugin = containerAppsRemediationPlugin;

        _containerAppPlugin = containerAppPlugin;
        _chartplugin = chartplugin;
        _githubIssuePlugin = githubIssuePlugin;
        _sourceCodePlugin = sourceCodePlugin;
    }

    // TODO: the userMessage is not needed as we are using the repository to get the messages
    public async Task<string> ProcessUserMessage(string userMessage, string threadId)
    {
        _log.LogInformation("[ChatThreadId {threadId}] Processing user message: {Message}", threadId, userMessage);
        using var _ = await _lock.AcquireWriterAsync();

        Guid threadGuid = Guid.Parse(threadId);
        var threadMessages = await _repository.GetMessagesAsync(threadGuid);
        var chatHistory = new List<ChatMessage> { new ChatMessage(ChatRole.System, SystemPrompt) };
        foreach (var msg in threadMessages)
        {
            ChatRole role = msg.Author.Role == Role.User ? ChatRole.User : ChatRole.Assistant;
            chatHistory.Add(new ChatMessage(role, msg.Text));
        }
        
        _tlsBestPracticesPlugin.ThreadId = threadId;
        _managedIdentityMigrationPlugin.ThreadId = threadId;
        _appServiceRemediationPlugin.ThreadId = threadId;
        _containerAppsRemediationPlugin.ThreadId = threadId;
        _sourceCodePlugin.ThreadId = threadId;

        var chartPluginDefinition = new ChartPluginDefinition(_chartplugin);
        _chartplugin.ThreadId = threadId;

        List <AITool> _aiTools =
        [
            AIFunctionFactory.Create(_managedIdentityMigrationPlugin.ListManagedIdentityMigrations),
            AIFunctionFactory.Create(_managedIdentityMigrationPlugin.StartManagedIdentityMigrationAgent),
            AIFunctionFactory.Create(_tlsBestPracticesPlugin.ListTlsBestPracticeWorkflows),
            AIFunctionFactory.Create(_tlsBestPracticesPlugin.StartTlsBestPracticeAgent),
            AIFunctionFactory.Create(_appServiceRemediationPlugin.ListAppServiceRemediationWorkflows),
            AIFunctionFactory.Create(_appServiceRemediationPlugin.StartAppServiceRemediationAgent),
            AIFunctionFactory.Create(_containerAppsRemediationPlugin.ListContainerAppsRemediationWorkflows),
            AIFunctionFactory.Create(_containerAppsRemediationPlugin.StartContainerAppsRemediationAgent),
            AIFunctionFactory.Create(_subscriptionPlugin.ListAllSubscriptionsAsync),
            AIFunctionFactory.Create(_subscriptionPlugin.ListAppServicesAsync),
            AIFunctionFactory.Create(_containerAppPlugin.ListContainerAppsAsync),
            AIFunctionFactory.Create(chartPluginDefinition.PlotPieChartAsync),
            AIFunctionFactory.Create(chartPluginDefinition.PlotBarChartAsync),
            AIFunctionFactory.Create(chartPluginDefinition.PlotTimeSeriesDataAsync),
            AIFunctionFactory.Create(_githubIssuePlugin.FetchGithubSecurityDependabotAlerts),
            AIFunctionFactory.Create(_sourceCodePlugin.ListSourceCodeWorkflows),
            AIFunctionFactory.Create(_sourceCodePlugin.StartSourceCodeAgent),
            AIFunctionFactory.Create(chartPluginDefinition.PlotTimeSeriesDataAsync)
        ];

        _aiTools.AddRange(_mcpToolsRepository.GetAllFunctions());

        var response = await _chatClient.GetResponseAsync(
            chatHistory,
            new ChatOptions
            {
                Tools = _aiTools,
                ToolMode = ChatToolMode.Auto,
                AdditionalProperties = new AdditionalPropertiesDictionary
                {
                    //["AllowParallelToolCalls"] = false,
                }
            });

        //// TODO - consider preserving tool call messages...
        return response.Messages.Last().Text;
    }
}