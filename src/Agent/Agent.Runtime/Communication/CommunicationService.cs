using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Communication;

public class CommunicationService : ICommunicationService
{
    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly ILogger<CommunicationService> _logger;

    public CommunicationService(
        IThreadOrchestrationManager mappingManager,
        ILogger<CommunicationService> logger)
    {
        _mappingManager = mappingManager;
        _logger = logger;
    }

    public async Task SendMessageAsync(string threadId, string message)
    {
        _logger.LogInformation("Agent message to thread {ThreadId}: {Message}", threadId, message);

        var mapping = await _mappingManager.GetMappingByThreadIdAsync(threadId);

        // TODO: Add this message to the DB and then the controller will poll for it
    }

    public async Task NotifyCompletionAsync(string threadId, string instanceId, string status, string? summary = null)
    {
        _logger.LogInformation("Orchestration {InstanceId} completed with status: {Status}", instanceId, status);

        var mapping = await _mappingManager.GetMappingByThreadIdAsync(threadId);
        if (mapping != null)
        {
            // Remove the mapping as the orchestration is completed
            await _mappingManager.RemoveMappingAsync(threadId);

            // TODO: Notify the user that the orchestration is completed
        }
    }
}