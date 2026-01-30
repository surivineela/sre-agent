// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Diagnostics.Metrics;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.AgentMemory;
using Agent.Framework;
using Agent.Framework.Skills;
using Agent.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Agent.Runtime.Reasoning;

public interface IReasoningLoopFactory
{
    Task<ReasoningLoop> Create(AgentContext context);
    Agent<AgentContext> GetAgent(string agentName, string? threadId = null);
}

public class ReasoningLoopFactory : IReasoningLoopFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly ILogger<ReasoningLoopFactory> _logger;
    private readonly IChatClientProvider _chatClientProvider;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly IStreamingMessageRepository _streamingMessageRepository;
    private readonly IAgentProvider<AgentContext> _agentProvider;
    private readonly IAgentRuntimeModifier<AgentContext> _agentRuntimeModifier;
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly IThreadRepository _threadRepository;
    private readonly ActionSettings _actionSettings;
    private readonly CoreSettings _coreSettings;
    private readonly IncidentManagementSettings _incidentManagementSettings;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly CustomerLogger _customerLogger;
    private readonly ISkillRegistry _skillRegistry;
    private readonly IToolOutputProcessService _toolOutputProcessService;
    private readonly IAgentFileStorageService _agentFileStorageService;
    private readonly IAmbientContextProvider _ambientContextProvider;

    private readonly Tracer _tracer;

    // regional search
    private readonly ISearchEndpointService _searchEndpointService;
    private readonly SearchHelper _searchHelper;

    // agent memory
    private readonly IAgentMemoryClient _agentMemoryClient;
    private readonly ISearchIndexService _searchIndexService;

    // experimental features
    private readonly FeatureConfigModel _featureConfig;
    private readonly bool _enableReasoningDebugOutput;

    public ReasoningLoopFactory(
        ILoggerFactory loggerFactory,
        IChatClientProvider chatClientProvider,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IStreamingMessageRepository streamingMessageRepository,
        IThreadRepository threadRepository,
        IAgentProvider<AgentContext> agentProvider,
        IToolFactory<AgentContext> toolFactory,
        AzureSettings azureSettings,
        IAgentRuntimeModifier<AgentContext> agentRuntimeModifier,
        ActionSettings actionSettings,
        CoreSettings coreSettings,
        Tracer tracer,
        CustomerLogger customerLogger,
        IHostEnvironment hostEnvironment,
        ISearchEndpointService searchEndpointService,
        SearchHelper searchHelper,
        IAgentMemoryClient agentMemoryClient,
        ISearchIndexService searchIndexService,
        IToolOutputProcessService toolOutputProcessService,
        IAgentFileStorageService agentFileStorageService,
        IMeterFactory meterFactory,
        IncidentManagementSettings incidentManagementSettings,
        ISkillRegistry skillRegistry,
        IAmbientContextProvider ambientContextProvider
        )
    {
        _loggerFactory = loggerFactory;
        _logger = _loggerFactory.CreateLogger<ReasoningLoopFactory>();
        _chatClientProvider = chatClientProvider;
        _outboundCommunicationService = outboundCommunicationService;
        _streamingMessageRepository = streamingMessageRepository;
        _agentProvider = agentProvider;
        _agentRuntimeModifier = agentRuntimeModifier;
        _threadRepository = threadRepository;
        _toolFactory = toolFactory;
        _actionSettings = actionSettings;
        _coreSettings = coreSettings;
        _hostEnvironment = hostEnvironment;
        _tracer = tracer;
        _customerLogger = customerLogger;
        _enableReasoningDebugOutput = coreSettings.EnableReasoningOutput
            && hostEnvironment.IsDevelopment(); // only enable debug output in dev environment
        _searchEndpointService = searchEndpointService;
        _searchHelper = searchHelper;
        _agentMemoryClient = agentMemoryClient;
        _searchIndexService = searchIndexService;
        _toolOutputProcessService = toolOutputProcessService;
        _agentFileStorageService = agentFileStorageService;
        _incidentManagementSettings = incidentManagementSettings;
        _skillRegistry = skillRegistry;
        _ambientContextProvider = ambientContextProvider;

        // enable handoff reasoning for developer envs
        var enableHandoffReasoning = coreSettings.Experimental?.EnableHandoffReasoning
            ?? hostEnvironment.IsDevelopment();

        _featureConfig = new FeatureConfigModel(
            AutoHandoffEnabled: coreSettings.Experimental?.AutoHandoffToMeta ?? false,
            RegionalSearchEnabled: azureSettings.SearchEndpoint.EnableDocumentRetrieval,
            AgentMemoryEnabled: coreSettings.AgentMemory.Enabled,
            TrajectoryRetrievalEnabled: coreSettings.AgentMemory.TrajectoryRetrievalEnabled,
            HandoffReasoningEnabled: enableHandoffReasoning,
            DocumentRetrievalEnabled: coreSettings.AgentMemory.DocumentRetrievalEnabled,
            UserMemoryRetrievalEnabled: coreSettings.AgentMemory.UserMemoryRetrievalEnabled,
            Gpt5Enabled: coreSettings.AgentModel?.GPT5Enabled ?? false,
            PartialOutputEnabled: coreSettings.Azure.ToolOutputSettings.EnablePartialOutput);
    }

    public async Task<ReasoningLoop> Create(AgentContext context)
    {
        // get the default start agent based on settings
        // Use meta_agent by default; allow overrides based on environment.
        var defaultStartingAgentName = "meta_agent";

        var agentType = Environment.GetEnvironmentVariable("AGENT_TYPE_NAME") ?? string.Empty;
        var agentName = AgentNameHelper.GetCustomerAgentName(_hostEnvironment.IsProduction());

        if (agentType == "ACAAgent")
        {
            defaultStartingAgentName = "rca_meta_agent";
        }
        else if (agentType == RcaRoutingConstants.AgentType)
        {
            // Use helper to select workflow vs conversation root agent.
            defaultStartingAgentName = ModeSwitchHelper.GetRcaRootAgent(context, _coreSettings);
        }
        else if (agentType == "FunctionsFlexConsumptionCRIAgent")
        {
            defaultStartingAgentName = "flex_consumption_cri_agent";
        }
        else if (agentType == "ColdStartAgent")
        {
            defaultStartingAgentName = "cold_start_agent";
        }

        // retrieve the current starting agent if present in context
        var currentStartingAgentName = defaultStartingAgentName;

        if (context.AgentHandoffChain.Count > 0)
        {
            // If the agent stack is provided, use the last agent in the stack
            currentStartingAgentName = context.AgentHandoffChain[^1];
        }
        else
        {
            if (context.CurrentAgent != null)
            {
                currentStartingAgentName = context.CurrentAgent;
            }

            context.AgentHandoffChain.Add(currentStartingAgentName);
        }

        var threadId = context.ThreadId.ToString();
        var defaultStartingAgent = _agentProvider.GetAgent(defaultStartingAgentName, threadId);
        var currentStartingAgent = _agentProvider.GetAgent(currentStartingAgentName, threadId);

        // update thread doc with enabled features on reasoning loop creation
        await UpdateThreadFeatureConfig(context.ThreadId);

        // Special handling for RCARouterAgent workflow orchestration
        // Check if we're dealing with a dispatched agent that's an Orchestrator type
        if (ModeSwitchHelper.UseWorkflowOrchestrator(agentType, context, _coreSettings) && !string.IsNullOrEmpty(currentStartingAgentName))
        {
            try
            {
                var dispatchedAgent = _agentProvider.GetAgent(currentStartingAgentName, threadId);
                _logger.LogInternalInformation($"Creating WorkflowOrchestrator for dispatched RCA agent: {currentStartingAgentName}");

                // Create WorkflowOrchestrator for the dispatched orchestrator agent
                var workflowOrchestrator = new WorkflowOrchestrator(
                    loggerFactory: _loggerFactory,
                    chatClientProvider: _chatClientProvider,
                    outboundCommunicationService: _outboundCommunicationService,
                    threadRepository: _threadRepository,
                    context: context,
                    agentProvider: _agentProvider,
                    toolFactory: _toolFactory,
                    tracer: _tracer,
                    incidentManagementSettings: _incidentManagementSettings,
                    coreSettings: _coreSettings,
                    skillRegistry: _skillRegistry);

                await workflowOrchestrator.LoadChatHistoryAsync();

                // Create a WorkflowReasoningLoop that delegates to the WorkflowOrchestrator
                return new WorkflowReasoningLoop(
                    workflowOrchestrator: workflowOrchestrator,
                    loggerFactory: _loggerFactory,
                    chatClientProvider: _chatClientProvider,
                    outboundCommunicationService: _outboundCommunicationService,
                    streamingMessageRepository: _streamingMessageRepository,
                    defaultStartingAgent: dispatchedAgent,
                    startingAgent: dispatchedAgent,
                    threadRepository: _threadRepository,
                    context: context,
                    toolFactory: _toolFactory,
                    actionSettings: _actionSettings,
                    tracer: _tracer,
                    customerLogger: _customerLogger,
                    agentProvider: _agentProvider,
                    enableReasoningDebugOutput: _enableReasoningDebugOutput,
                    searchEndpointService: _searchEndpointService,
                    searchHelper: _searchHelper,
                    agentMemoryClient: _agentMemoryClient,
                    searchIndexService: _searchIndexService,
                    agentMemorySettings: _coreSettings.AgentMemory,
                    featureConfig: _featureConfig,
                    agentRuntimeModifier: _agentRuntimeModifier,
                    incidentManagementSettings: _incidentManagementSettings,
                    coreSettings: _coreSettings,
                    modeSwitchEnabled: ModeSwitchHelper.ModeSwitchEnabled(_coreSettings),
                    skillRegistry: _skillRegistry,
                    toolOutputProcessService: _toolOutputProcessService,
                    agentFileStorageService: _agentFileStorageService,
                    hostEnvironment: _hostEnvironment,
                    ambientContextProvider: _ambientContextProvider);

            }
            catch (Exception ex)
            {
                _logger.LogInternalWarning(ex, $"Failed to create WorkflowOrchestrator for dispatched agent {currentStartingAgentName}, falling back to standard ReasoningLoop");
            }
        }

        // Create and return a new instance of ReasoningLoop
        var loop = new ReasoningLoop(
            loggerFactory: _loggerFactory,
            chatClientProvider: _chatClientProvider,
            outboundCommunicationService: _outboundCommunicationService,
            streamingMessageRepository: _streamingMessageRepository,
            defaultStartingAgent: defaultStartingAgent,
            startingAgent: currentStartingAgent,
            threadRepository: _threadRepository,
            context: context,
            toolFactory: _toolFactory,
            actionSettings: _actionSettings,
            tracer: _tracer,
            customerLogger: _customerLogger,
            agentProvider: _agentProvider,
            enableReasoningDebugOutput: _enableReasoningDebugOutput,
            searchEndpointService: _searchEndpointService,
            searchHelper: _searchHelper,
            agentMemoryClient: _agentMemoryClient,
            searchIndexService: _searchIndexService,
            agentMemorySettings: _coreSettings.AgentMemory,
            featureConfig: _featureConfig,
            agentRuntimeModifier: _agentRuntimeModifier,
            toolOutputProcessService: _toolOutputProcessService,
            agentFileStorageService: _agentFileStorageService,
            hostEnvironment: _hostEnvironment,
            modeSwitchEnabled: ModeSwitchHelper.ModeSwitchEnabled(_coreSettings),
            skillRegistry: _skillRegistry,
            ambientContextProvider: _ambientContextProvider);

        await loop.LoadChatHistoryAsync();
        return loop;
    }

    public Agent<AgentContext> GetAgent(string agentName, string? threadId = null)
    {
        return _agentProvider.GetAgent(agentName, threadId);
    }

    private Task UpdateThreadFeatureConfig(Guid threadId)
    {
        return _threadRepository.UpdateThreadFeatureSetAsync(
            threadId: threadId,
            featureConfig: _featureConfig.ToDocument());
    }
}



