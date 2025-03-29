using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Plugins.Definitions;
using Microsoft.Bot.Schema;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Communication;

public class OutboundCommunicationService : IAgentOutboundCommunicationService
{

    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly IThreadRepository _repository;
    private readonly ILogger<OutboundCommunicationService> _logger;


    private readonly IPostToTeamsPlugin _postToTeamsService;

    public OutboundCommunicationService(
        IThreadOrchestrationManager mappingManager,
        IThreadRepository repository,
        ILogger<OutboundCommunicationService> logger,
        IPostToTeamsPlugin postToTeamsService)
    {
        _mappingManager = mappingManager;
        _repository = repository;
        _logger = logger;
        _postToTeamsService = postToTeamsService;
    }

    public async Task UpdateThreadWithAgentMessageAsync(string threadId, string orchestrationInstanceId, ChatMessage message)
    {
        var agentId = "sre-agent";
        if (!string.IsNullOrEmpty(orchestrationInstanceId))
        {
            await _mappingManager.AddMappingAsync(new ThreadOrchestrationMapping(
                Id: $"mapping_{threadId}",
                ThreadId: threadId,
                OrchestrationInstanceId: orchestrationInstanceId,
                CreatedTimestamp: DateTime.UtcNow,
                ModifiedTimestamp: DateTime.UtcNow
                )
            );
            agentId = orchestrationInstanceId;
        }
        _logger.LogInformation("orchestrationInstanceId {orchestrationInstanceId} message to thread {ThreadId}: {Message}",
            orchestrationInstanceId, threadId, message.Text);

        if (Guid.TryParse(threadId, out var guidThreadId))
        {
            // Save to repository
            var dbMessage = new Message(
                Id: Guid.NewGuid(),
                TimeStamp: DateTime.UtcNow,
                Author: new Author(Role.SREAgent, agentId, "Azure SRE Agent"),
                Text: message.Text ?? string.Empty
            );

            await _repository.AddMessageAsync(guidThreadId, dbMessage);
        }
        else
        {
            _logger.LogWarning("Invalid thread ID format: {ThreadId}", threadId);
        }
    }

    public async Task<Guid> AppendAgentImageMessage(Guid threadId, string message)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }
        var messageId = Guid.NewGuid();
        var agentMessage = new Message(
            Id: messageId,
            TimeStamp: DateTime.UtcNow,
            Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
            IsImageContent: true,
            Text: message
        );

        await _repository.AddMessageAsync(threadId, agentMessage);

        return messageId;
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