using Agent.Core;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;
using Agent.Data.Repositories;
using Microsoft.Extensions.Logging;
using Agent.Runtime.Communication;
using Agent.Plugins;
using Agent.Core.Interfaces;

namespace Agent.Runtime.MetaAgent;

// [Export]
public sealed class MetaAgent : IAgent
{
    private const string SystemPrompt = @"# Azure SRE Agent

You are a specialized Azure SRE Agent designed to assist users with Microsoft Azure products and services.

Your primary role is to understand user requests and delegate tasks to appropriate sub-agents when necessary.

You are part of a multi-agent system for Azure SRE Agent, designed to make agent coordination and execution easy.
Agents uses two primary abstraction: **Agents** and **Handoffs**.
An agent encompasses instructions and tools and can hand off a conversation to another agent when appropriate.
Handoffs are achieved by calling a handoff function, generally named `start<agent_name>agent`.
Transfers between agents are handled seamlessly in the background; do not mention or draw attention to these transfers in your conversation with the user.

## Primary Capabilities
- **App Service Remediation**: If there is any issue with Azure WebApps or Azure Function apps, you delegate to this plugin which supports monitoring application health metrics, analyzing application issues like high cpu, network miss configuration, memory leaks and carrying out operations to remediate these apps
- **Managed Identity Migration**: Help users migrate from certificate-based authentication to managed identities
- **TLS Best Practices**: Guide users in implementing TLS best practices for Azure resources

## Core Responsibilities
1. **Request Triage**: Determine if a user request is related to Azure SRE concerns
2. **Sub-Agent Delegation**: Route requests to specialized sub-agents when appropriate
3. **Workflow Management**: Start, monitor, and summarize various Azure-related workflows

## Response Protocol
- Maintain focus exclusively on Microsoft Azure products and services
- Decline to respond to non-Azure related queries with a polite redirection
- When delegating to sub-agents, clearly communicate the handoff process to users
- Provide concise, actionable responses formatted according to Microsoft Teams guidelines

## Operation Framework
When handling Azure SRE requests, follow this general pattern:

1. **List**: Provide users with available options and workflows relevant to their query
2. **Summarize**: Explain details of a specific option when requested or selected
3. **Start**: Initiate the appropriate workflow by delegating to specialized sub-agents

This framework applies to all Azure SRE operations, allowing you to:
- Help users discover available capabilities
- Provide detailed information before taking action
- Seamlessly transition to specialized sub-agents for execution

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
    private readonly List<AIFunction> _aiTools = new();
    private readonly AsyncReaderWriterLock _lock = new();

    private readonly IChatClient _chatClient;
    private readonly ILogger<MetaAgent> _log;

    public MetaAgent(
        IChatClient chatClient,
        ILogger<MetaAgent> logger,
        IThreadRepository repository,
        IThreadOrchestrationManager mappingManager,
        IAgentOutboundCommunicationService outboundCommunicationService,
        ManagedIdentityMigrationPlugin managedIdentityMigrationPlugin,
        TlsBestPracticesPlugin tlsBestPracticesPlugin,
        AppServiceRemediationPlugin appServiceRemediationPlugin,
        ISubscriptionPlugin subscriptionPlugin)
    {
        _chatClient = chatClient;
        _repository = repository;
        _mappingManager = mappingManager;
        _outboundCommunicationService = outboundCommunicationService;
        _log = logger;

        // Please make sure you categorize the output of these tools correctly below into:
        // - NewOrchestration
        // - ReusingOrchestration
        // - General questions to be answered by the meta-agent or list all orchestrations
        _aiTools.Add(AIFunctionFactory.Create(managedIdentityMigrationPlugin.ListManagedIdentityMigrations));
        _aiTools.Add(AIFunctionFactory.Create(managedIdentityMigrationPlugin.SummarizeManagedIdentityMigration));
        _aiTools.Add(AIFunctionFactory.Create(managedIdentityMigrationPlugin.StartManagedIdentityMigrationAgent));
        _aiTools.Add(AIFunctionFactory.Create(tlsBestPracticesPlugin.ListTlsBestPracticeWorkflows));
        _aiTools.Add(AIFunctionFactory.Create(tlsBestPracticesPlugin.SummarizeTlsBestPractice));
        _aiTools.Add(AIFunctionFactory.Create(tlsBestPracticesPlugin.StartTlsBestPracticeAgent));
        _aiTools.Add(AIFunctionFactory.Create(appServiceRemediationPlugin.StartAppServiceRemediationAgent));
        _aiTools.Add(AIFunctionFactory.Create(appServiceRemediationPlugin.SummarizeAppServiceRemidiationWorkflow));
        _aiTools.Add(AIFunctionFactory.Create(appServiceRemediationPlugin.ListAppServiceRemediationWorkflows));
        _aiTools.Add(AIFunctionFactory.Create(subscriptionPlugin.ListAllSubscriptionsAsync));
        _aiTools.Add(AIFunctionFactory.Create(subscriptionPlugin.ListAppServicesAsync));
    }

    // TODO: the userMessage is not needed as we are using the repository to get the messages
    public async Task<string> ProcessUserMessage(string userMessage, string threadId)
    {
        _log.LogInformation("[ChatThreadId {threadId}] Processing user message: {Message}", threadId, userMessage);
        using var _ = await _lock.AcquireWriterAsync();
        Guid threadGuid = Guid.Parse(threadId);
        var threadMessages = await _repository.GetMessagesAsync(threadGuid);
        var _chatHistory = new List<ChatMessage> { new ChatMessage(ChatRole.System, SystemPrompt) };
        foreach (var msg in threadMessages)
        {
            ChatRole role = msg.Author.Role == Role.User ? ChatRole.User : ChatRole.Assistant;
            _chatHistory.Add(new ChatMessage(role, msg.Text));
        }

        while (true)
        {
            var response = await _chatClient.GetResponseAsync(
                _chatHistory,
                new ChatOptions
                {
                    Tools = _aiTools.Select<AIFunction, AITool>(x => x).ToList(),
                    ToolMode = ChatToolMode.Auto,
                    AdditionalProperties = new AdditionalPropertiesDictionary
                    {
                        ["AllowParallelToolCalls"] = true,
                        ["ThreadId"] = threadId, // Pass threadId in additional properties
                    }
                });

            _log.LogInformation("[ChatThreadId {threadId}] Getting intermediate response: {Message}", threadId, response.Message);

            // Add model response back to ChatHistory
            // TODO: do we add the intermediate responses to the repository?
            _chatHistory.Add(response.Message);

            var results = new List<AIContent>();
            foreach (var fnCall in response.Message.Contents.OfType<FunctionCallContent>())
            {
                var matchingTool = _aiTools.Single(x => x.Name == fnCall.Name);

                var category = "unknown";
                switch (fnCall.Name)
                {
                    // Category 1: NewOrchestration - return value is always orchestration id
                    case nameof(ManagedIdentityMigrationPlugin.StartManagedIdentityMigrationAgent):
                    case nameof(TlsBestPracticesPlugin.StartTlsBestPracticeAgent):
                    case nameof(AppServiceRemediationPlugin.StartAppServiceRemediationAgent):
                        category = "NewOrchestration";
                        break;

                    // Category 2: ReusingOrchestration - requires instanceId (orchestration id) as parameter
                    case nameof(ManagedIdentityMigrationPlugin.SummarizeManagedIdentityMigration):
                    case nameof(TlsBestPracticesPlugin.SummarizeTlsBestPractice):
                    case nameof(AppServiceRemediationPlugin.SummarizeAppServiceRemidiationWorkflow):
                        category = "ReusingOrchestration";
                        break;

                    // Category 3: General questions - handled by meta-agent or list all orchestrations
                    case nameof(ManagedIdentityMigrationPlugin.ListManagedIdentityMigrations):
                    case nameof(TlsBestPracticesPlugin.ListTlsBestPracticeWorkflows):
                    case nameof(AppServiceRemediationPlugin.ListAppServiceRemediationWorkflows):
                    default:
                        category = "General";
                        break;
                }


                // Create a new dictionary with the thread ID if needed
                IDictionary<string, object?> arguments = fnCall.Arguments;
                if (!string.IsNullOrEmpty(threadId))
                {
                    // Make a copy of the arguments and add the threadId
                    arguments = new Dictionary<string, object?>(fnCall.Arguments);

                    // Inject threadId to the arguments to avoid hallucination
                    arguments["threadId"] = threadId;

                    if (category == "ReusingOrchestration")
                    {
                        if (arguments["instanceId"] == null)
                        {
                            _log.LogError("[ChatThreadId {threadId}] ReusingOrchestration function call: {Name} requires instanceId, but it's not provided. Inject the default one to avoid exception.", threadId, fnCall.Name);
                            var mapping = await _mappingManager.GetMappingsByThreadIdAsync(threadId);
                            var instanceId = mapping.FirstOrDefault()?.OrchestrationInstanceId;
                            if (instanceId == null)
                            {
                                _log.LogError("[ChatThreadId {threadId}] ReusingOrchestration function call: {Name} requires instanceId, but orchestration found in the thread.", threadId, fnCall.Name);
                                continue;
                            }
                            else
                            {
                                arguments["instanceId"] = instanceId;
                            }
                        }
                    }
                }

                // Invoke function call with potentially modified arguments
                var invokeResult = await matchingTool.InvokeAsync(arguments);
                var result = new FunctionResultContent(fnCall.CallId, invokeResult);

                if (category == "NewOrchestration")
                {
                    // Extract instanceId from the result
                    var resString = invokeResult?.ToString() ?? string.Empty;
                    var instanceId = resString.Split(' ').Last();
                    _log.LogInformation("[ChatThreadId {threadId}] NewOrchestration function call: {Name}, Orchestration ID: {result}", threadId, fnCall.Name, instanceId);
                    await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(threadId, instanceId, new ChatMessage(ChatRole.Assistant, $"This request has been delegated to a specialized sub-agent with orchestration instance ID: {instanceId}."));
                }

                results.Add(result);

                _log.LogInformation("[ChatThreadId {threadId}] Getting function call [{Name}] in [category {category}] response: {Message}", threadId, fnCall.Name, category, response.Message);

            }

            if (results.Count > 0)
            {
                // Add function call response, and re-evaluate the ChatHistory with model
                // TODO: do we add the intermediate tools responses to the repository?
                _chatHistory.Add(new ChatMessage(ChatRole.Tool, results));
            }
            else
            {
                // When model has no function call response, we assume it's a single text response
                // We return this response to user
                _log.LogInformation("[ChatThreadId {threadId}] Resolved with final response: {Message}", threadId, response.Message.Contents.OfType<TextContent>().Single().Text);
                return response.Message.Contents.OfType<TextContent>().Single().Text;
            }
        }
    }
}