// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Framework;
using Agent.Runtime.Models;
using Agent.Runtime.Services;
using Agent.Core.Models.Api.v1;
using Agent.Core.Interfaces;
using Microsoft.Extensions.AI;

namespace Agent.Runtime;

/// <summary>
/// Implementation of IDisplayModelOutput that streams content to the outbound communication service
/// </summary>
public class ChatMessageOutput : IDisplayModelOutput
{
    private readonly IAgentOutboundCommunicationService _outboundCommunicationService;
    private readonly AgentContext _context;
    private Guid? _messageId;

    public ChatMessageOutput(
        IAgentOutboundCommunicationService outboundCommunicationService,
        AgentContext context)
    {
        _outboundCommunicationService = outboundCommunicationService ?? throw new ArgumentNullException(nameof(outboundCommunicationService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _messageId = null;
    }

    public ChatMessageOutput(
        IAgentOutboundCommunicationService outboundCommunicationService,
        AgentContext context,
        Guid messageId)
    {
        _outboundCommunicationService = outboundCommunicationService ?? throw new ArgumentNullException(nameof(outboundCommunicationService));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _messageId = messageId;
    }

    /// <summary>
    /// Displays content by sending it through the outbound communication service
    /// </summary>
    /// <param name="content">The content to display</param>
    /// <returns>A task representing the asynchronous operation</returns>
    public Task OnDisplay(string content)
    {
        return _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            _context,
            new ChatMessage(ChatRole.Assistant, content),
            _messageId,
            false);
    }

    public async Task OnComplete(string? content, ChatFinishReason? chatFinishReason)
    {
        if (_messageId == null)
        {
            return;
        }

        await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            _context,
            new ChatMessage(ChatRole.Assistant, content),
                _messageId, true);


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

        await _outboundCommunicationService.UpdateThreadWithAgentMessageAsync(
            _context,
            new ChatMessage(ChatRole.Assistant, ""),
                _messageId, true);
        _messageId = null;
    }
}
