// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using Agent.Core.Configuration; // for CoreSettings / feature flag
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI;

namespace Agent.Runtime.Reasoning;

public interface IReasoningLoopManager
{
    Task AppendNewMessageAsync(AgentContext context, ChatMessage msg, ConversationModifierEnum? conversationModifier = null, CancellationToken cancellationToken = default);
    Task AppendFunctionCallMessagesAsync(AgentContext context, List<ChatMessage> msgs, CancellationToken cancellationToken = default);
    void CancelCurrentOperation(AgentContext context);
    Task SetHomeAgentAsync(AgentContext context, string agentName);
    Task<IEnumerable<ChatMessage>> ExportChatHistory(AgentContext agentContext, CancellationToken token = default);
    Task NotifyApprovalDecisionAsync(AgentContext context, Approval approval, CancellationToken cancellationToken = default);
}

public class ReasoningLoopManager : IReasoningLoopManager
{
    private readonly IReasoningLoopFactory _reasoningLoopFactory;
    private readonly ConcurrentDictionary<Guid, ReasoningLoop> _reasoningLoops = [];
    private readonly CoreSettings _coreSettings; // injected to read mode switch feature flag

    public ReasoningLoopManager(IReasoningLoopFactory reasoningLoopFactory, CoreSettings coreSettings)
    {
        _reasoningLoopFactory = reasoningLoopFactory;
        _coreSettings = coreSettings;
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

    public async Task SetHomeAgentAsync(AgentContext context, string agentName)
    {
        var loop = await GetOrCreateReasoningLoopAsync(context);
        await loop.SetHomeAgent(agentName);
    }

    public async Task<IEnumerable<ChatMessage>> ExportChatHistory(AgentContext context, CancellationToken cancellationToken = default)
    {
        var loop = await GetOrCreateReasoningLoopAsync(context);
        return await loop.ExportChatHistoryAsync(cancellationToken);
    }

    private async Task<ReasoningLoop> GetOrCreateReasoningLoopAsync(AgentContext context)
    {
        // Feature-flag gated dynamic loop swap. This executes ONLY when the mode switch feature is enabled.
        // If disabled, or no swap needed, this helper returns null and we fall through to the original block below (kept untouched).
        var swapped = await TrySwapLoopForModeSwitchAsync(context);
        if (swapped != null)
        {
            return swapped; // early return only when feature flag true and a mode-driven swap occurred
        }

        if (!_reasoningLoops.TryGetValue(context.ThreadId, out var loop))
        {
            loop = await _reasoningLoopFactory.Create(context);
            _reasoningLoops[context.ThreadId] = loop;
        }
        return loop;
    }

    /// <summary>
    /// Attempt to swap the loop implementation when /mode conversation|workflow changes the desired execution model.
    /// Runs ONLY when the mode switch feature flag is enabled. Returns null if no swap is performed.
    /// </summary>
    private async Task<ReasoningLoop?> TrySwapLoopForModeSwitchAsync(AgentContext context)
    {
        // Guard: feature flag check. If flag off we never interfere with original code path.
        if (!ModeSwitchHelper.ModeSwitchEnabled(_coreSettings))
        {
            return null;
        }

        // If no existing loop, let original creation logic run (returns null here).
        if (!_reasoningLoops.TryGetValue(context.ThreadId, out var existing))
        {
            return null;
        }

        // Determine desired loop type based on current agent mode.
        // Workflow: when AgentMode equals "workflow" (case-insensitive). Otherwise conversation mode uses standard ReasoningLoop.
        var desiredWorkflow = string.Equals(context.AgentMode, nameof(ReasoningMode.Workflow), StringComparison.OrdinalIgnoreCase);
        var haveWorkflow = existing is WorkflowReasoningLoop;

        // If already correct type, keep existing.
        if (desiredWorkflow == haveWorkflow)
        {
            return existing;
        }

        // Need to swap: remove and dispose old loop, then recreate via factory (which itself respects current mode).
        if (_reasoningLoops.TryRemove(context.ThreadId, out var removed))
        {
            try { removed.Dispose(); } catch { /* swallow dispose errors */ }
        }

        try
        {
            var newLoop = await _reasoningLoopFactory.Create(context);
            await newLoop.LoadChatHistoryAsync();
            _reasoningLoops[context.ThreadId] = newLoop;
            return newLoop;
        }
        catch
        {
            // On failure, fallback to existing instance if still valid; otherwise null so caller uses original path.
            if (existing != null)
            {
                _reasoningLoops[context.ThreadId] = existing; // ensure dictionary retains a loop
                return existing;
            }
            return null;
        }
    }

    public void CancelCurrentOperation(AgentContext context)
    {
        // Check if thread ID exists before attempting removal
        if (_reasoningLoops.TryGetValue(context.ThreadId, out var loop))
        {
            loop.CancelCurrentOperation();
        }
    }
}
