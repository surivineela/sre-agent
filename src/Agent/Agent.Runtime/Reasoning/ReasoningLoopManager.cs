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
    Task NotifyApprovalDecisionAsync(AgentContext context, Approval approval, CancellationToken cancellationToken = default);
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
        var loop = await GetOrCreateReasoningLoopAsync(context);
        await loop.AppendNewMessageAsync(msg, cancellationToken);
    }

    public async Task NotifyApprovalDecisionAsync(AgentContext context, Approval approval, CancellationToken cancellationToken = default)
    {
        var loop = await GetOrCreateReasoningLoopAsync(context);
        await loop.NotifyApprovalDecisionAsync(approval, cancellationToken);
    }

    private async Task<ReasoningLoop> GetOrCreateReasoningLoopAsync(AgentContext context)
    {
        if (!_reasoningLoops.TryGetValue(context.ThreadId, out var loop))
        {
            loop = await _reasoningLoopFactory.Create(context);
            _reasoningLoops[context.ThreadId] = loop;
        }

        return loop;
    }
}
