using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Runtime.MetaAgent;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Agent.Runtime.Models;
using Agent.Runtime.Services;

namespace Agent.Runtime.Communication;

public class InboundCommunicationService : IAgentInboundCommunicationService
{
    private readonly IAgent _metaAgent;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly IThreadRepository _repository;
    private readonly ILogger<InboundCommunicationService> _logger;
    private readonly SinkService _sinkService;
    private readonly ThreadService _threadService;

    public InboundCommunicationService(
        IAgent metaAgent,
        DurableTaskClient durableTaskClient,
        IThreadOrchestrationManager mappingManager,
        IThreadRepository repository,
        SinkService sinkService,
        ThreadService threadService,
        ILogger<InboundCommunicationService> logger)
    {
        _metaAgent = metaAgent;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
        _repository = repository;
        _sinkService = sinkService;
        _threadService = threadService;
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

    public async Task<Guid> AppendAgentImageMessage(Guid threadId, string message)
    {
        if (threadId == Guid.Empty)
        {
            throw new ArgumentException("Thread ID cannot be empty.", nameof(threadId));
        }

        return await _sinkService.SinkAgentMessageAsync(threadId, message, isImageContent: true);
    }

    public async Task<InboundServiceResponse> ProcessUserMessageAsync(ThreadMessage message)
    {
        try
        {
            await _sinkService.SinkUserMessageAsync(message);
            string orchestrationInstanceId = "";
            Guid responseMessageId = Guid.Empty;

            // Check if an orchestration already exists for this thread
            ThreadContext threadContext = new ThreadContext(message.ThreadId);
            orchestrationInstanceId = await _threadService.GetOrchestrationInstanceId(threadContext);

            if (!string.IsNullOrEmpty(orchestrationInstanceId))
            {

                var existingOrchestration = await _durableTaskClient.GetInstanceAsync(orchestrationInstanceId,
                    getInputsAndOutputs: true, CancellationToken.None);
                // Check for failed orchestrations and clean them if needed
                bool cleaned = await _threadService.CleanOrchestration(
                    threadContext,
                    orchestrationInstanceId,
                    existingOrchestration);

                // If the orchestration was cleaned, get the updated orchestration ID (might be empty now)
                if (cleaned)
                {
                    orchestrationInstanceId = await _threadService.GetOrchestrationInstanceId(threadContext);
                }
            }

            if (string.IsNullOrEmpty(orchestrationInstanceId))
            {
                var threadMessages = await _repository.GetMessagesAsync(message.ThreadId);

                // No existing orchestration, create a new one
                _logger.LogInformation("No existing orchestration for thread: {ThreadId}", message.ThreadId);
                // Process the message with MetaAgent
                string agentResponse = await _metaAgent.ProcessUserMessage(threadContext);

                responseMessageId = await _sinkService.SinkAgentMessageAsync(message.ThreadId, agentResponse);
            }
            else
            {
                // TODO (jianbosun): 
                // For now, we assume there's only 1:1 mapping for threadId and orchestrationInstanceId,
                // but we may change this to allow multiple orchestrations per thread, e.g. to choose sub-agent type in one thread as a different orchestration.
                // This will enable us for scenarios that need to share chat history with multiple orchestrations for different purposes.

                // Existing orchestration, raise an event to it
                _logger.LogInformation("Sending message to existing orchestration for thread: {ThreadId}", message.ThreadId);
                await _durableTaskClient.RaiseEventAsync(
                    orchestrationInstanceId,
                    "NewChatMessage",
                    new ChatMessage(ChatRole.User, message.Message));
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