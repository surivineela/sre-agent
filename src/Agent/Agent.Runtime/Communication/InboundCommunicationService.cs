using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Runtime.MetaAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Communication;

public class InboundCommunicationService : IAgentInboundCommunicationService
{
    private readonly IAgent _metaAgent;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly IThreadRepository _repository;
    private readonly ILogger<InboundCommunicationService> _logger;

    public InboundCommunicationService(
        IAgent metaAgent,
        DurableTaskClient durableTaskClient,
        IThreadOrchestrationManager mappingManager,
        IThreadRepository repository,
        ILogger<InboundCommunicationService> logger)
    {
        _metaAgent = metaAgent;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
        _repository = repository;
        _logger = logger;
    }

    public async Task<Core.Models.Api.v1.Thread> CreateAgentThread(string title, string message)
    {
        var now = DateTime.UtcNow;
        var thread = new Core.Models.Api.v1.Thread
        (
            Id: Guid.NewGuid(),
            Title: title,
            new Message
            (
                Guid.NewGuid(),
                now,
                new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
                message
            ),
            now,
            now
        );

        // TODO - how should we share implementation with process user message and make sure fan out occurs?
        await _repository.CreateThreadAsync(thread);
        await _repository.AddMessageAsync(thread.Id, thread.StartMessage);

        return thread;
    }

    public async Task<InboundServiceResponse> ProcessUserMessageAsync(ThreadMessage message)
    {
        try
        {
            var aiMessage = new Message(
                Id: message.MessageId,
                TimeStamp: DateTime.UtcNow,
                Author: new Author(Role.User, message.UserId, message.DisplayName),
                Text: message.Message
            );

            await _repository.AddMessageAsync(message.ThreadId, aiMessage);
            string orchestrationInstanceId = "";
            Guid responseMessageId = Guid.Empty;

            // Check if an orchestration already exists for this thread
            var mappings = await _mappingManager.GetMappingsByThreadIdAsync(message.ThreadId.ToString());
            if (mappings == null || !mappings.Any())
            {
                // No existing orchestration, create a new one
                _logger.LogInformation("No existing orchestration for thread: {ThreadId}", message.ThreadId);
                // Process the message with MetaAgent
                string agentResponse = await _metaAgent.ProcessUserMessage(
                    message.Message,
                    message.ThreadId.ToString());
                responseMessageId = Guid.NewGuid();
                var responseMessage = new Message(
                    Id: responseMessageId,
                    TimeStamp: DateTime.UtcNow,
                    Author: new Author(Role.SREAgent, "agent-default", "Azure SRE Agent"),
                    Text: agentResponse);
                await _repository.AddMessageAsync(message.ThreadId, responseMessage);
            }
            else
            {
                // TODO (jianbosun): 
                // For now, we assume there's only 1:1 mapping for threadId and orchestrationInstanceId,
                // but we may change this to allow multiple orchestrations per thread, e.g. to choose sub-agent type in one thread as a different orchestration.
                // This will enable us for scenarios that need to share chat history with multiple orchestrations for different purposes.
                var mapping = mappings.FirstOrDefault();
                // Existing orchestration, raise an event to it
                _logger.LogInformation("Sending message to existing orchestration for thread: {ThreadId}", message.ThreadId);
                await _durableTaskClient.RaiseEventAsync(
                    mapping.OrchestrationInstanceId,
                    "NewChatMessage",
                    new ChatMessage(ChatRole.User, message.Message));

                orchestrationInstanceId = mapping.OrchestrationInstanceId;
            }
            return new InboundServiceResponse(message.ThreadId, responseMessageId, orchestrationInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing user message for thread: {ThreadId}", message.ThreadId);
            throw;
        }
    }


}