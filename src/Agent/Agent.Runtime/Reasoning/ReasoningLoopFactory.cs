// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Reasoning;

public interface IReasoningLoopFactory
{
    Task<ReasoningLoop> Create(AgentContext context);
}

public class ReasoningLoopFactory : IReasoningLoopFactory
{
    private readonly ILogger<ReasoningLoopFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IChatClient _chatClient;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly IAgentFactory<AgentContext> _agentFactory;
    private readonly IToolFactory _toolFactory;
    private readonly IThreadRepository _threadRepository;

    public ReasoningLoopFactory(
        ILogger<ReasoningLoopFactory> logger,
        ILoggerFactory loggerFactory,
        IChatClient chatClient,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IThreadRepository threadRepository,
        IAgentFactory<AgentContext> agentFactory,
        IToolFactory toolFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _chatClient = chatClient;
        _outboundCommunicationService = outboundCommunicationService;
        _agentFactory = agentFactory;
        _threadRepository = threadRepository;
        _toolFactory = toolFactory;
    }

    public async Task<ReasoningLoop> Create(AgentContext context)
    {
        var agent = _agentFactory.GetAgent(context.CurrentAgent ?? "meta_agent");
        // Create and return a new instance of ReasoningLoop
        var loop = new ReasoningLoop(
            _loggerFactory.CreateLogger<ReasoningLoop>(),
            _loggerFactory,
            _chatClient,
            _outboundCommunicationService,
            agent,
            _threadRepository,
            context,
            _toolFactory);

        await loop.LoadChatHistoryAsync();
        return loop;
    }
}
