// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Framework;
using Microsoft.Extensions.AI;

namespace Agent.Runtime;

/// <summary>
/// Implementation of IDisplayModelOutput that streams content to the outbound communication service
/// </summary>
public class ChatMessageOutput : IDisplayModelOutput
{
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly IStreamingMessageRepository _streamingMessageRepository;
    private readonly AgentContext _context;
    private Guid? _messageId;

    public ChatMessageOutput(
        IAgentOutboundCommunicationService outboundCommunicationService,
        IStreamingMessageRepository streamingMessageRepository,
        AgentContext context,
        Guid messageId)
    {
        _outboundCommunicationService = outboundCommunicationService ?? throw new ArgumentNullException(nameof(outboundCommunicationService));
        _streamingMessageRepository = streamingMessageRepository ?? throw new ArgumentNullException(nameof(streamingMessageRepository));
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

    public async Task OnComplete(string content = "")
    {
        // Mark as complete and save to DB through outbound service
        await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            _context,
            new ChatMessage(ChatRole.Assistant, content),
            _messageId,
            isComplete: true);

        // when message completes, reset messageGuid for next messages
        _messageId = Guid.NewGuid();
    }

    public async Task OnIncomplete()
    {
        if (_messageId == null)
        {
            return;
        }

        // Remove from in-memory storage without persisting to DB
        await _streamingMessageRepository.DeleteMessageAsync(_context.ThreadId, _messageId.Value);
        _messageId = null;
    }
}
