// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.Extensions.AI;
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
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly IAgentFactory<AgentContext> _agentFactory;
    private readonly IToolFactory<AgentContext> _toolFactory;
    private readonly IThreadRepository _threadRepository;
    private readonly ActionSettings _actionSettings;

    private readonly Tracer _tracer;

    public ReasoningLoopFactory(
        ILoggerFactory loggerFactory,
        IChatClient chatClient,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IThreadRepository threadRepository,
        IAgentFactory<AgentContext> agentFactory,
        IToolFactory<AgentContext> toolFactory,
        ActionSettings actionSettings,
        Tracer tracer)
    {
        _loggerFactory = loggerFactory;
        _chatClient = chatClient;
        _outboundCommunicationService = outboundCommunicationService;
        _agentFactory = agentFactory;
        _threadRepository = threadRepository;
        _toolFactory = toolFactory;
        _actionSettings = actionSettings;
        _tracer = tracer;
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
            _loggerFactory.CreateLogger<ReasoningLoop>(),
            _loggerFactory,
            _chatClient,
            _outboundCommunicationService,
            agent,
            _threadRepository,
            context,
            _toolFactory,
            _actionSettings,
            _tracer,
            _agentFactory);

        await loop.LoadChatHistoryAsync();
        return loop;
    }
}
