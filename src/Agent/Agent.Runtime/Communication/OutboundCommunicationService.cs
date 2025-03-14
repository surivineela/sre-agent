using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Communication;

public class OutboundCommunicationService : IAgentOutboundCommunicationService
{
    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly IThreadRepository _repository;
    private readonly ILogger<OutboundCommunicationService> _logger;

    public OutboundCommunicationService(
        IThreadOrchestrationManager mappingManager,
        IThreadRepository repository,
        ILogger<OutboundCommunicationService> logger)
    {
        _mappingManager = mappingManager;
        _repository = repository;
        _logger = logger;
    }

    public async Task UpdateThreadWithAgentMessageAsync(string threadId, string orchestrationInstanceId, ChatMessage message)
    {
        var agentId = "sre-agent";
        if (!string.IsNullOrEmpty(orchestrationInstanceId))
        {
            await _mappingManager.AddMappingAsync(new ThreadOrchestrationMapping
            {
                ThreadId = threadId,
                OrchestrationInstanceId = orchestrationInstanceId
            });
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

    public async Task NotifyCompletionAsync(string threadId, string orchestrationInstanceId, string status, string? summary = null)
    {
        _logger.LogInformation("orchestrationInstanceId {orchestrationInstanceId} completed with status: {Status}", orchestrationInstanceId, status);

        var mapping = await _mappingManager.GetMappingsByThreadIdAsync(threadId);
        if (mapping.Any())
        {
            // Create completion message
            var message = $"Task completed with status: {status}";
            if (!string.IsNullOrEmpty(summary))
            {
                message = $"{message}\n\n{summary}";
            }

            // Save completion message
            await UpdateThreadWithAgentMessageAsync(
                threadId,
                orchestrationInstanceId,
                new ChatMessage(ChatRole.Assistant, message));

            // Remove the mapping as the orchestration is completed
            await _mappingManager.RemoveMappingAsync(threadId, orchestrationInstanceId);
        }
    }
}