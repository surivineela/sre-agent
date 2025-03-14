using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Runtime.MetaAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Communication;

public class CommunicationService : ISubAgentInboundCommunicationService, ISubAgentOutboundCommunicationService
{
    private readonly IAgent _metaAgent;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly IThreadRepository _repository;
    private readonly ILogger<CommunicationService> _logger;

    public CommunicationService(
        IAgent metaAgent,
        DurableTaskClient durableTaskClient,
        IThreadOrchestrationManager mappingManager,
        IThreadRepository repository,
        ILogger<CommunicationService> logger)
    {
        _metaAgent = metaAgent;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
        _repository = repository;
        _logger = logger;
    }

    public async Task<string> ProcessUserMessageAsync(ThreadMessage message)
    {
        try
        {
            // Check if an orchestration already exists for this thread
            var mapping = await _mappingManager.GetMappingByThreadIdAsync(message.ThreadId);

            if (mapping == null)
            {
                // No existing orchestration, create a new one
                _logger.LogInformation("No existing orchestration for thread: {ThreadId}", message.ThreadId);

                // Process the message with MetaAgent
                string agentResponse = await _metaAgent.ProcessUserMessage(
                    message.Message,
                    message.ThreadId);

                return agentResponse;
            }
            else
            {
                // Existing orchestration, raise an event to it
                _logger.LogInformation("Sending message to existing orchestration for thread: {ThreadId}", message.ThreadId);
                await _durableTaskClient.RaiseEventAsync(
                    mapping.OrchestrationInstanceId,
                    "NewChatMessage",
                    new ChatMessage(ChatRole.User, message.Message));

                return "Message forwarded to agent";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user message for thread: {ThreadId}", message.ThreadId);
            throw;
        }
    }

    public async Task UpdateThreadWithAgentMessageAsync(string threadId, string agentId, ChatMessage message)
    {
        _logger.LogInformation("Agent {AgentId} message to thread {ThreadId}: {Message}",
            agentId, threadId, message.Text);

        if (Guid.TryParse(threadId, out var guidThreadId))
        {
            // Save to repository
            var dbMessage = new Message(
                Id: Guid.NewGuid(),
                TimeStamp: DateTime.UtcNow,
                Author: new Author(Role.SREAgent, agentId, "Azure SRE Agent"),
                Text: message.Text
            );

            await _repository.AddMessageAsync(guidThreadId, dbMessage);
        }
        else
        {
            _logger.LogWarning("Invalid thread ID format: {ThreadId}", threadId);
        }
    }

    public async Task NotifyCompletionAsync(string threadId, string agentId, string status, string? summary = null)
    {
        _logger.LogInformation("Agent {AgentId} completed with status: {Status}", agentId, status);

        var mapping = await _mappingManager.GetMappingByThreadIdAsync(threadId);
        if (mapping != null)
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
                agentId,
                new ChatMessage(ChatRole.Assistant, message));

            // Remove the mapping as the orchestration is completed
            await _mappingManager.RemoveMappingAsync(threadId);
        }
    }
}