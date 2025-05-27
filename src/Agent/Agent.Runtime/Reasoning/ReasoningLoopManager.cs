using System;
using System.Collections.Concurrent;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Reasoning;

public interface IReasoningLoopManager
{
    Task AppendNewMessageAsync(AgentContext context, ChatMessage msg, CancellationToken cancellationToken = default);
}

public class ReasoningLoopManager : IReasoningLoopManager
{
    private readonly IReasoningLoopFactory _reasoningLoopFactory;
    private readonly ConcurrentDictionary<Guid, ReasoningLoop> _reasoningLoops;

    public ReasoningLoopManager(IReasoningLoopFactory reasoningLoopFactory)
    {
        _reasoningLoopFactory = reasoningLoopFactory;
        _reasoningLoops = new ConcurrentDictionary<Guid, ReasoningLoop>();
    }

    public async Task AppendNewMessageAsync(AgentContext context, ChatMessage msg, CancellationToken cancellationToken = default)
    {
        var threadId = context.ThreadId;
        if (!_reasoningLoops.ContainsKey(threadId))
        {
            _reasoningLoops[threadId] = await _reasoningLoopFactory.Create(context);
        }

        var loop = _reasoningLoops[threadId];

        await loop.AppendNewMessage(msg, cancellationToken);
    }
}