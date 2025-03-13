using Agent.Runtime.MetaAgent;
using Agent.Runtime.Communication;
using Microsoft.DurableTask.Client;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Communication;

public class UserMessageService
{
    private readonly IAgent _metaAgent;
    private readonly DurableTaskClient _durableTaskClient;
    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly ICommunicationService _communicationService;
    private readonly ILogger<UserMessageService> _logger;

    public UserMessageService(
        IAgent metaAgent,
        DurableTaskClient durableTaskClient,
        IThreadOrchestrationManager mappingManager,
        ICommunicationService communicationService,
        ILogger<UserMessageService> logger)
    {
        _metaAgent = metaAgent;
        _durableTaskClient = durableTaskClient;
        _mappingManager = mappingManager;
        _communicationService = communicationService;
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
                _logger.LogInformation("Creating new orchestration for thread: {ThreadId}", message.ThreadId);

                // Generate a predictable orchestration ID based on thread ID
                string orchestrationInstanceId = $"thread-{message.ThreadId}-{Guid.NewGuid()}";

                // First, create the mapping in memory
                // This will be in cosmos in prod
                var newMapping = new ThreadOrchestrationMapping
                {
                    ThreadId = message.ThreadId,
                    OrchestrationInstanceId = orchestrationInstanceId,
                    CreatedAt = DateTime.UtcNow,
                };

                await _mappingManager.AddMappingAsync(newMapping);

                // Now, process the message with the known thread Id
                string agentResponse = await _metaAgent.ProcessUserMessage(
                    message.Message,
                    message.ThreadId);

                // Response is sent by MetaAgent directly
                await _communicationService.SendMessageAsync(message.ThreadId, agentResponse);

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
}
