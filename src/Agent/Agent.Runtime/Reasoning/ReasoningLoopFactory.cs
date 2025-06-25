// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.AgentMemory;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.Services;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
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
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly IThreadRepository _threadRepository;
    private readonly ActionSettings _actionSettings;
    private readonly IAgentActionLogExporter _actionLogExporter;
    private readonly bool _enableReasoningDebugOutput;
    private readonly ISearchEndpointService _searchEndpointService;
    private readonly bool _enableDocumentRetrieval;
    private readonly bool _enableVectorSearch;

    private readonly Tracer _tracer;
    private readonly IAgentMemoryClient _agentMemoryClient;
    private readonly bool _agentMemoryEnabled;

    public ReasoningLoopFactory(
        ILoggerFactory loggerFactory,
        IChatClient chatClient,
        IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IThreadRepository threadRepository,
        IAgentFactory<AgentContext> agentFactory,
        IToolFactory<AgentContext> toolFactory,
        AzureSettings azureSettings,
        ActionSettings actionSettings,
        CoreSettings coreSettings,
        Tracer tracer,
        IAgentActionLogExporter actionLogExporter,
        IHostEnvironment hostEnvironment,
        ISearchEndpointService searchEndpointService,
        IAgentMemoryClient agentMemoryClient,
        AgentMemorySettings agentMemorySettings)
    {
        _loggerFactory = loggerFactory;
        _chatClient = chatClient;
        _embeddingGenerator = embeddingGenerator;
        _outboundCommunicationService = outboundCommunicationService;
        _agentFactory = agentFactory;
        _threadRepository = threadRepository;
        _toolFactory = toolFactory;
        _actionSettings = actionSettings;
        _tracer = tracer;
        _actionLogExporter = actionLogExporter;
        _enableReasoningDebugOutput = coreSettings.EnableReasoningOutput
            && hostEnvironment.IsDevelopment(); // only enable debug output in dev environment
        _searchEndpointService = searchEndpointService;
        _enableDocumentRetrieval = azureSettings.SearchEndpoint.EnableDocumentRetrieval;
        _enableVectorSearch = azureSettings.SearchEndpoint.EnableVectorSearch;
        _agentMemoryClient = agentMemoryClient;
        _agentMemoryEnabled = agentMemorySettings.Enabled;
    }

    public async Task<ReasoningLoop> Create(AgentContext context)
    {
        var agentName = "meta_agent";

        var agentType = Environment.GetEnvironmentVariable("AGENT_TYPE_NAME") ?? string.Empty;
        if (agentType == "ACAAgent")
        {
            agentName = "rca_meta_agent";
        }
        if (context.AgentHandoffChain.Count > 0)
        {
            // If the agent stack is provided, use the last agent in the stack
            agentName = context.AgentHandoffChain[^1];
        }
        else
        {
            if (context.CurrentAgent != null)
            {
                agentName = context.CurrentAgent;
            }

            context.AgentHandoffChain.Add(agentName);
        }
        var agent = _agentFactory.GetAgent(agentName);

        // Create and return a new instance of ReasoningLoop
        var loop = new ReasoningLoop(
            loggerFactory: _loggerFactory,
            chatClient: _chatClient,
            embeddingGenerator: _embeddingGenerator,
            outboundCommunicationService: _outboundCommunicationService,
            startingAgent: agent,
            threadRepository: _threadRepository,
            context: context,
            toolFactory: _toolFactory,
            actionSettings: _actionSettings,
            tracer: _tracer,
            agentFactory: _agentFactory,
            actionLogExporter: _actionLogExporter,
            enableReasoningDebugOutput: _enableReasoningDebugOutput,
            searchEndpointService: _searchEndpointService,
            enableDocumentRetrieval: _enableDocumentRetrieval,
            enableVectorSearch: _enableVectorSearch,
            agentMemoryClient: _agentMemoryClient,
            agentMemoryEnabled: _agentMemoryEnabled);

        await loop.LoadChatHistoryAsync();
        return loop;
    }
}
