// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;
using AIChatMessage = Microsoft.Extensions.AI.ChatMessage;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
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
    public async Task<string> GetOrchestrationInstanceId(ThreadContext threadContext)
    {
        var mappings = (await _mappingManager.GetMappingsByThreadIdAsync(threadContext.ThreadId.ToString())).ToList();
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
        ThreadContext threadContext,
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
            string failureMessage = $"Orchestration id {orchestrationInstanceId} mapped to thread {threadContext.ThreadId} " +
                $"has failed with runtime status {existingOrchestration.RuntimeStatus}.";
            _logger.LogWarning(failureMessage);

            await _mappingManager.RemoveMappingAsync(threadContext.ThreadId.ToString(), orchestrationInstanceId);

            // Notify about the failed orchestration
            await _sinkService.SinkAgentMessageAsync(threadContext, failureMessage);

            try
            {
                var finalState = existingOrchestration.ReadCustomStatusAs<string>();
                if (!string.IsNullOrEmpty(finalState))
                {
                    _logger.LogInformation($"Final state of orchestration {orchestrationInstanceId}: {finalState}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error reading final state of orchestration {orchestrationInstanceId}");
            }

            return true;
        }

        return false;
    }

    /// <summary>
    /// Converts the thread messages to LLM chat history format, it just list user requests and agent response.
    /// Messages like SystemPrompt, PluginLog, or image content will be excluded.
    /// </summary>
    /// <returns>A list of chat messages in the format expected by the LLM.</returns>
    public async Task<List<AIChatMessage>> ToChatHistory(ThreadContext threadContext)
    {
        var ThreadMessages = await _threadRepository.GetMessagesAsync(threadContext.ThreadId);
        List<AIChatMessage> chatHistory = [];

        foreach (var msg in ThreadMessages)
        {
            // Skip messages with roles other than User, SREAgent, or System
            ChatRole role;
            switch (msg.Author.Role)
            {
                case Role.User:
                    role = ChatRole.User;
                    break;
                case Role.SREAgent:
                    role = ChatRole.Assistant;
                    break;
                default:
                    continue;  // Skip this message if role is PluginLog or not recognized
            }
            if (msg.IsImageContent)
            {
                continue;
            }
            chatHistory.Add(new AIChatMessage(role, msg.Text));
        }

        return chatHistory;
    }

    public async Task<List<AIChatMessage>> ToLLMChatHistory(ThreadContext threadContext, string systemPrompt)
    {
        List<AIChatMessage> chatHistory = [new AIChatMessage(ChatRole.System, systemPrompt)];
        chatHistory.AddRange(await ToChatHistory(threadContext));
        return chatHistory;
    }

    public async Task<string> GetLastUserMessage(ThreadContext threadContext)
    {
        var ThreadMessages = await _threadRepository.GetMessagesAsync(threadContext.ThreadId);
        var lastUserMessage = ThreadMessages.LastOrDefault(m => m.Author.Role == Role.User);
        return lastUserMessage?.Text ?? string.Empty;
    }
}
