// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

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
    private readonly ConcurrentDictionary<Guid, ReasoningLoop> _reasoningLoops = [];

    public ReasoningLoopManager(IReasoningLoopFactory reasoningLoopFactory)
    {
        _reasoningLoopFactory = reasoningLoopFactory;
    }

    public async Task AppendNewMessageAsync(AgentContext context, ChatMessage msg, CancellationToken cancellationToken = default)
    {
        var threadId = context.ThreadId;

        if (!_reasoningLoops.TryGetValue(threadId, out var loop))
        {
            loop = await _reasoningLoopFactory.Create(context);
            _reasoningLoops[threadId] = loop;
        }

        await loop.AppendNewMessage(msg, cancellationToken);
    }
}
