// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models; // ReasoningMessage, enums
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.AI; // ChatMessage, ChatRole

namespace Agent.Runtime.Reasoning;

/// <summary>
/// Instance handler that encapsulates /mode conversation|workflow switching logic.
/// Keeps calling sites (ReasoningLoop / WorkflowOrchestrator) minimal (single method call).
/// </summary>
internal sealed class ModeSwitchHandler
{
    private readonly IThreadRepository _threadRepository;
    private readonly IAgentOutboundCommunicationService _outbound;
    private readonly bool _enabled;

    public ModeSwitchHandler(
        IThreadRepository threadRepository,
        IAgentOutboundCommunicationService outboundCommunicationService,
        bool enabled)
    {
        _threadRepository = threadRepository;
        _outbound = outboundCommunicationService;
        _enabled = enabled;
    }

    /// <summary>
    /// Processes a raw user text for /mode switching.
    /// Returns handled=true if a mode switch (or already-in-mode notification) occurred; caller should then stop further processing.
    /// </summary>
    public async Task<(bool handled, AgentContext updated)> HandleAsync(
        AgentContext context,
        string? rawText,
        CancellationToken ct)
    {
        if (!_enabled || string.IsNullOrWhiteSpace(rawText))
        {
            return (false, context);
        }

        if (!ModeCommandParser.TryParse(rawText, out var targetModeRaw, out var initialUserUtterance))
        {
            return (false, context);
        }

        var targetMode = targetModeRaw.Equals("conversation", StringComparison.OrdinalIgnoreCase)
            ? nameof(ReasoningMode.Conversation)
            : nameof(ReasoningMode.Workflow);

        if (string.Equals(context.AgentMode, targetMode, StringComparison.OrdinalIgnoreCase))
        {
            await SendSystemAsync(context, $"Already in {targetModeRaw} mode.");
            return (true, context);
        }

        var newChain = new List<string>
        {
            targetMode == nameof(ReasoningMode.Conversation)
                ? RcaRoutingConstants.ConversationRootAgent
                : RcaRoutingConstants.WorkflowRootAgent
        };

        var updated = context with
        {
            AgentMode = targetMode,
            AgentHandoffChain = newChain,
            CurrentAgent = newChain[0]
        };

        updated = await _threadRepository.UpdateAgentContextAsync(updated);
        await SendSystemAsync(updated, $"Mode changed to {targetModeRaw}.");

        if (!string.IsNullOrWhiteSpace(initialUserUtterance))
        {
            var msg = new ChatMessage(ChatRole.User, initialUserUtterance);
            var reasoningMessage = new ReasoningMessage(
                Id: Guid.NewGuid(),
                AgentContextId: updated.Id,
                Role: ReasoningMessageRoleEnum.User,
                SerializedChatMessage: JsonSerializer.Serialize(msg));
            await _threadRepository.CreateReasoningMessageAsync(reasoningMessage);
            var hist = await _threadRepository.GetAgentChatHistoryAsync(updated.Id);
            if (hist != null)
            {
                await _threadRepository.AddReasoningMessagesToChatHistoryAsync(hist, reasoningMessage);
            }
        }

        return (true, updated);
    }

    private Task SendSystemAsync(AgentContext ctx, string text)
        => _outbound.UpdateThreadWithAgentMessageAsync(ctx, new ChatMessage(ChatRole.System, text));
}
