// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Plugins.Definitions;
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

    public OutboundCommunicationService(
        IThreadOrchestrationManager mappingManager,
        ILogger<OutboundCommunicationService> logger,
        IPostToTeamsPlugin postToTeamsService,
        SinkService sinkService)
    {
        _mappingManager = mappingManager;
        _logger = logger;
        _postToTeamsService = postToTeamsService;
        _sinkService = sinkService;
    }

    public async Task UpdateThreadWithAgentMessageAsync(ThreadContext? threadContext, string orchestrationInstanceId, ChatMessage message)
    {
        var threadId = threadContext?.ThreadId.ToString() ?? string.Empty;
        if (!string.IsNullOrEmpty(orchestrationInstanceId))
        {
            await _mappingManager.AddMappingAsync(threadId, orchestrationInstanceId);
        }
        _logger.LogInformation("orchestrationInstanceId {orchestrationInstanceId} message to thread {ThreadId}: {Message}",
            orchestrationInstanceId, threadId, message.Text);

        await _sinkService.SinkAgentMessageAsync(threadContext, message.Text ?? string.Empty);
    }

    public async Task<Guid> AppendAgentImageMessage(ThreadContext threadContext, string message)
    {
        if (threadContext == null || threadContext.ThreadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadContext));
        }

        // Use SinkService to add the image message
        return await _sinkService.SinkAgentMessageAsync(threadContext, message, true);
    }

    public async Task NotifyCompletionAsync(string threadId, string orchestrationInstanceId, string status, string? summary = null)
    {
        _logger.LogInformation("orchestrationInstanceId {orchestrationInstanceId} completed with status: {Status}", orchestrationInstanceId, status);

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
}
