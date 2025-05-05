// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Runtime.Communication;
using Microsoft.Extensions.Logging;
using Microsoft.DurableTask.Client;

namespace Agent.Runtime.Services;

// ThreadService will always take ThreadContext as a parameter.
public class ThreadService
{
    private readonly IThreadRepository _threadRepository;
    private readonly IThreadOrchestrationManager _mappingManager;
    private readonly ILogger<ThreadService> _logger;
    private readonly SinkService _sinkService;

    public ThreadService(
     ILogger<ThreadService> logger,
     IThreadRepository threadRepository,
     IThreadOrchestrationManager mappingManager,
     SinkService sinkService)
    {
        _logger = logger;
        _threadRepository = threadRepository;
        _mappingManager = mappingManager;
        _sinkService = sinkService;
    }

    /// <summary>
    /// Gets the orchestration instance ID for a given thread context.
    /// </summary>
    /// <param name="threadContext">The thread context.</param>
    /// <returns>The orchestration instance ID if exists, otherwise an empty string.</returns>
    public async Task<string> GetOrchestrationInstanceId(Guid threadId)
    {
        var mappings = (await _mappingManager.GetMappingsByThreadIdAsync(threadId.ToString())).ToList();
        return mappings?.FirstOrDefault()?.OrchestrationInstanceId ?? string.Empty;
    }

    /// <summary>
    /// Checks if an orchestration is in a bad state and cleans it up if needed.
    /// </summary>
    /// <param name="threadContext">The thread context.</param>
    /// <param name="orchestrationInstanceId">The orchestration instance ID to check.</param>
    /// <param name="durableTaskClient">The durable task client.</param>
    /// <param name="sinkService">The sink service for sending messages.</param>
    /// <returns>True if the orchestration was in a bad state and was cleaned, otherwise false.</returns>
    /// <returns>True if the orchestration was in a bad state and was cleaned, otherwise false.</returns>
    public async Task<bool> CleanOrchestration(
        Guid threadId,
        string orchestrationInstanceId,
        OrchestrationMetadata existingOrchestration)
    {
        if (string.IsNullOrEmpty(orchestrationInstanceId) || existingOrchestration == null)
        {
            return false;
        }

        // Check if the orchestration is in a failed state
        if (existingOrchestration != null && existingOrchestration.IsCompleted &&
            existingOrchestration.RuntimeStatus != OrchestrationRuntimeStatus.Completed)
        {
            string failureMessage = $"Orchestration id {orchestrationInstanceId} mapped to thread {threadId} " +
                $"has failed with runtime status {existingOrchestration.RuntimeStatus}.";
            _logger.LogInternalWarning(failureMessage);

            await _mappingManager.RemoveMappingAsync(threadId.ToString(), orchestrationInstanceId);

            // Notify about the failed orchestration
            await _sinkService.SinkAgentMessageAsync(threadId, failureMessage);

            try
            {
                var finalState = existingOrchestration.ReadCustomStatusAs<string>();
                if (!string.IsNullOrEmpty(finalState))
                {
                    _logger.LogInternalInformation($"Final state of orchestration {orchestrationInstanceId}: {finalState}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Error reading final state of orchestration {orchestrationInstanceId}");
            }

            return true;
        }

        return false;
    }

    public async Task<string> GetLastUserMessage(Guid threadId)
    {
        var ThreadMessages = await _threadRepository.GetMessagesAsync(threadId);
        var lastUserMessage = ThreadMessages.LastOrDefault(m => m.Author.Role == Role.User);
        return lastUserMessage?.Text ?? string.Empty;
    }
}
