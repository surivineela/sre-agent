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
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;

namespace Agent.Runtime.Reasoning;

public interface IReasoningLoopFactory
{
    Task<ReasoningLoop> Create(AgentContext context);
}

public class ReasoningLoopFactory : IReasoningLoopFactory
{
    private readonly ILoggerFactory _loggerFactory;
    private readonly IChatClient _chatClient;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly IAgentFactory<AgentContext> _agentFactory;
    private readonly IAgentRuntimeModifier<AgentContext> _agentRuntimeModifier;
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly IThreadRepository _threadRepository;
    private readonly ActionSettings _actionSettings;
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
        IChatClient chatClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IThreadRepository threadRepository,
        IAgentFactory<AgentContext> agentFactory,
        IToolFactory<AgentContext> toolFactory,
        AzureSettings azureSettings,
        IAgentRuntimeModifier<AgentContext> AgentRuntimeModifier,
        ActionSettings actionSettings,
        CoreSettings coreSettings,
        Tracer tracer,
        IHostEnvironment hostEnvironment,
        ISearchEndpointService searchEndpointService,
        SearchHelper searchHelper,
        IAgentMemoryClient agentMemoryClient,
        ISearchIndexService searchIndexService,
        IMeterFactory meterFactory)
    {
        _loggerFactory = loggerFactory;
        _chatClient = chatClient;
        _embeddingGenerator = embeddingGenerator;
        _outboundCommunicationService = outboundCommunicationService;
        _agentFactory = agentFactory;
        _agentRuntimeModifier = AgentRuntimeModifier;
        _threadRepository = threadRepository;
        _toolFactory = toolFactory;
        _actionSettings = actionSettings;
        _tracer = tracer;
        _enableReasoningDebugOutput = coreSettings.EnableReasoningOutput
            && hostEnvironment.IsDevelopment(); // only enable debug output in dev environment
        _searchEndpointService = searchEndpointService;
        _searchHelper = searchHelper;
        _agentMemoryClient = agentMemoryClient;
        _searchIndexService = searchIndexService;

        _featureConfig = new FeatureConfigModel(
            AutoHandoffEnabled: coreSettings.Experimental?.AutoHandoffToMeta ?? false,
            RegionalSearchEnabled: azureSettings.SearchEndpoint.EnableDocumentRetrieval,
            AgentMemoryEnabled: coreSettings.AgentMemory.Enabled,
            TrajectoryRetrievalEnabled: coreSettings.AgentMemory.TrajectoryRetrievalEnabled,
            HandoffReasoningEnabled: coreSettings.Experimental?.EnableHandoffReasoning ?? false);
    }

    public async Task<ReasoningLoop> Create(AgentContext context)
    {
        // get the default start agent based on settings
        var defaultStartingAgentName = "meta_agent";
        var agentType = Environment.GetEnvironmentVariable("AGENT_TYPE_NAME") ?? string.Empty;
        if (agentType == "ACAAgent")
        {
            defaultStartingAgentName = "rca_meta_agent";
        }
        else if (agentType == "RCARouterAgent")
        {
            defaultStartingAgentName = "rca_router_meta_agent";
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

        var defaultStartingAgent = _agentFactory.GetAgent(defaultStartingAgentName);
        var currentStartingAgent = _agentFactory.GetAgent(currentStartingAgentName);

        // update thread doc with enabled features on reasoning loop creation
        await UpdateThreadFeatureConfig(context.ThreadId);

        // Create and return a new instance of ReasoningLoop
        var loop = new ReasoningLoop(
            loggerFactory: _loggerFactory,
            chatClient: _chatClient,
            embeddingGenerator: _embeddingGenerator,
            outboundCommunicationService: _outboundCommunicationService,
            defaultStartingAgent: defaultStartingAgent,
            startingAgent: currentStartingAgent,
            threadRepository: _threadRepository,
            context: context,
            toolFactory: _toolFactory,
            actionSettings: _actionSettings,
            tracer: _tracer,
            agentFactory: _agentFactory,
            enableReasoningDebugOutput: _enableReasoningDebugOutput,
            searchEndpointService: _searchEndpointService,
            searchHelper: _searchHelper,
            agentMemoryClient: _agentMemoryClient,
            searchIndexService: _searchIndexService,
            featureConfig: _featureConfig,
            agentRuntimeModifier: _agentRuntimeModifier);

        await loop.LoadChatHistoryAsync();
        return loop;
    }

    private Task UpdateThreadFeatureConfig(Guid threadId)
    {
        return _threadRepository.UpdateThreadFeatureSetAsync(
            threadId: threadId,
            featureConfig: _featureConfig.ToDocument());
    }
}
