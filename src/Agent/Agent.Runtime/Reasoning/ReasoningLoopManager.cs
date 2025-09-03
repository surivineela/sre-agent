// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Reasoning;

public interface IReasoningLoopManager
{
    Task AppendNewMessageAsync(AgentContext context, ChatMessage msg, ConversationModifierEnum? conversationModifier = null, CancellationToken cancellationToken = default);
    Task AppendFunctionCallMessagesAsync(AgentContext context, List<ChatMessage> msgs, CancellationToken cancellationToken = default);
    void CancelCurrentOperation(AgentContext context);
    Task SetCurrentAgentAsync(AgentContext context, string agentName);
    Task<IEnumerable<ChatMessage>> ExportChatHistory(AgentContext agentContext, CancellationToken token = default);
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

    public async Task AppendNewMessageAsync(AgentContext context, ChatMessage msg, ConversationModifierEnum? conversationModifier = null, CancellationToken cancellationToken = default)
    {
        var loop = await GetOrCreateReasoningLoopAsync(context);
        await loop.AppendNewUserMessageAsync(msg, conversationModifier, cancellationToken);
    }

    public async Task AppendFunctionCallMessagesAsync(AgentContext context, List<ChatMessage> msgs, CancellationToken cancellationToken = default)
    {
        var loop = await GetOrCreateReasoningLoopAsync(context);
        await loop.AppendFunctionCallMessagesAsync(msgs, cancellationToken);
    }

    public async Task NotifyApprovalDecisionAsync(AgentContext context, Approval approval, CancellationToken cancellationToken = default)
    {
        var loop = await GetOrCreateReasoningLoopAsync(context);
        await loop.AppendNewApprovalMessageAsync(approval, cancellationToken);
    }

    public async Task SetCurrentAgentAsync(AgentContext context, string agentName)
    {
        var loop = await GetOrCreateReasoningLoopAsync(context);
        await loop.SetCurrentAgent(agentName);
    }

    public async Task<IEnumerable<ChatMessage>> ExportChatHistory(AgentContext context, CancellationToken cancellationToken = default)
    {
        var loop = await GetOrCreateReasoningLoopAsync(context);
        return await loop.ExportChatHistoryAsync(cancellationToken);
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

    public void CancelCurrentOperation(AgentContext context)
    {
        if (_reasoningLoops.TryRemove(context.ThreadId, out var loop))
        {
            loop.CancelCurrentOperation();
            // Dispose the loop since it's no longer needed
            loop.Dispose();
        }
        else
        {
            throw new InvalidOperationException($"No reasoning loop found for thread ID {context.ThreadId}");
        }
    }
}
