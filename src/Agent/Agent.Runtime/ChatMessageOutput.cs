// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Agent.Runtime.Communication;
using Agent.Runtime.Models;
using Microsoft.Extensions.AI;

namespace Agent.Runtime;

/// <summary>
/// Implementation of IDisplayModelOutput that streams content to the outbound communication service
/// </summary>
public class ChatMessageOutput : IDisplayModelOutput
{
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly InMemoryMessageStorageService _inMemoryMessageService;
    private readonly AgentContext _context;
    private Guid? _messageId;

    public ChatMessageOutput(
        IAgentOutboundCommunicationService outboundCommunicationService,
        InMemoryMessageStorageService inMemoryMessageService,
        AgentContext context,
        Guid messageId)
    {
        _outboundCommunicationService = outboundCommunicationService ?? throw new ArgumentNullException(nameof(outboundCommunicationService));
        _inMemoryMessageService = inMemoryMessageService ?? throw new ArgumentNullException(nameof(inMemoryMessageService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _messageId = messageId;
    }

    /// <summary>
    /// Displays content by sending it through the outbound communication service
    /// </summary>
    /// <param name="content">The content to display</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public async Task OnDisplay(string content)
    {
        await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            _context,
            new ChatMessage(ChatRole.Assistant, content),
            _messageId,
            isComplete: false);
    }

    public async Task OnComplete(string? content, ChatFinishReason? chatFinishReason)
    {
        // Mark as complete and save to DB through outbound service
        await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            _context,
            new ChatMessage(ChatRole.Assistant, content),
            _messageId,
            isComplete: true);

        if (chatFinishReason == ChatFinishReason.ToolCalls)
        {
            _messageId = Guid.NewGuid();
        }
        else if (chatFinishReason == ChatFinishReason.Stop)
        {
            _messageId = null;
        }
    }

    public async Task OnIncomplete()
    {
        if (_messageId == null)
        {
            return;
        }

        // Remove from in-memory storage without persisting to DB
        await _inMemoryMessageService.DeleteMessageAsync(_context.ThreadId, _messageId);
        _messageId = null;
    }
}
