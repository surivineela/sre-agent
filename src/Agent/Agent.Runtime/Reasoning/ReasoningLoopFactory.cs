using System;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Reasoning;

public interface IReasoningLoopFactory
{
    ReasoningLoop Create(AgentContext context);
}

public class ReasoningLoopFactory : IReasoningLoopFactory
{
    private readonly ILogger<ReasoningLoopFactory> _logger;
    private readonly ILoggerFactory _loggerFactory;
    private readonly IChatClient _chatClient;
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly IAgentFactory<AgentContext> _agentFactory;
    private readonly IThreadRepository _threadRepository;

    public ReasoningLoopFactory(
        ILogger<ReasoningLoopFactory> logger,
        ILoggerFactory loggerFactory,
        IChatClient chatClient,
        IAgentOutboundCommunicationService outboundCommunicationService,
        IThreadRepository threadRepository,
        IAgentFactory<AgentContext> agentFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
        _chatClient = chatClient;
        _outboundCommunicationService = outboundCommunicationService;
        _agentFactory = agentFactory;
        _threadRepository = threadRepository;
    }

    public ReasoningLoop Create(AgentContext context)
    {
        // Create and return a new instance of ReasoningLoop
        return new ReasoningLoop(
            _loggerFactory.CreateLogger<ReasoningLoop>(),
            _loggerFactory,
            _chatClient,
            _outboundCommunicationService,
            _agentFactory.GetAgent("meta_agent"),
            _threadRepository,
            context);
    }
}
