// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Plugins.Interface;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Communication;

public class OutboundCommunicationService : IAgentOutboundCommunicationService
{
    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly ILogger<OutboundCommunicationService> _logger;
    private readonly IPostToTeamsPlugin _postToTeamsService;
    private readonly SinkService _sinkService;
    private readonly IStreamingService _streamingService;

    public OutboundCommunicationService(
        IThreadOrchestrationManager mappingManager,
        ILogger<OutboundCommunicationService> logger,
        IPostToTeamsPlugin postToTeamsService,
        SinkService sinkService,
        IStreamingService streamingService)
    {
        _mappingManager = mappingManager;
        _logger = logger;
        _postToTeamsService = postToTeamsService;
        _sinkService = sinkService;
        _streamingService = streamingService;
    }

    public async Task UpdateThreadWithAgentMessageAsync(Guid? threadId, string orchestrationInstanceId, ChatMessage message)
    {
        if (!string.IsNullOrEmpty(orchestrationInstanceId))
        {
            await _mappingManager.AddMappingAsync(threadId.ToString(), orchestrationInstanceId);
        }
        _logger.LogExternalInformation("orchestrationInstanceId {orchestrationInstanceId} message to thread {ThreadId}: {Message}",
            orchestrationInstanceId, threadId, message.Text);

        await _sinkService.SinkAgentMessageAsync(threadId.Value, message.Text ?? string.Empty);
    }

    public async Task<Guid> AppendAgentImageMessage(Guid threadId, string message)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        // Use SinkService to add the image message
        return await _sinkService.SinkAgentMessageAsync(threadId, message, true);
    }

    public async Task<Guid> AppendAgentApprovalMessage(Guid threadId, Approval approval)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        // Use SinkService to add the image message
        return await _sinkService.SinkAgentMessageAsync(threadId, "Approval Request for Processing Azure SRE Agent Request", true, approval);
    }

    public async Task AppendAgentStreamMessage(Guid threadId, string message, StreamMessageType type)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        try
        {
            // Use the streaming service abstraction to send the message
            await _streamingService.StreamMessageAsync(threadId, message, type);

            _logger.LogExternalInformation("Successfully sent direct stream message for thread {ThreadId} with type {Type}", 
                threadId, type);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to stream message directly for thread {ThreadId}", threadId);
        }
    }

    public async Task NotifyCompletionAsync(string threadId, string orchestrationInstanceId, string status, string? summary = null)
    {
        _logger.LogInternalInformation("orchestrationInstanceId {orchestrationInstanceId} completed with status: {Status}", orchestrationInstanceId, status);

        var mapping = await _mappingManager.GetMappingsByThreadIdAsync(threadId);
        if (mapping.Any())
        {
            // todo - once meta agent context is separate from thread history, consider appending a message to the meta agent context so it knows that control has transferred back

            // Remove the mapping as the orchestration is completed
            await _mappingManager.RemoveMappingAsync(threadId, orchestrationInstanceId);
        }
    }

    public async Task PostActivity(string threadId, Activity activity, string messageId = "")
    {
        await _postToTeamsService.PostTeamsMessage(threadId, activity, messageId);
    }

    public Task UpdateThreadWithAgentMessageAsync(AgentContext context, ChatMessage message)
    {
        _logger.LogExternalInformation("Agent context {AgentContextId} of type {AgentType} message to thread {ThreadId}: {message}",
            context.Id, context.AgentType.ToString(), context.ThreadId, message.Text);

        return _sinkService.SinkAgentMessageAsync(context.ThreadId, message.Text ?? string.Empty);
    }

    public Task NotifyCompletionAsync(AgentContext context, string subAgentIdentifier, string status, string? summary = null)
    {
        var message = $"{subAgentIdentifier} completed with status: {status}";

        if (!string.IsNullOrEmpty(summary))
        {
            message += $" summary: {summary}";
        }

        return UpdateThreadWithAgentMessageAsync(context, new(ChatRole.Assistant, message));
    }
}
