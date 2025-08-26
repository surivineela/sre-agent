// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Runtime.Communication;
using Microsoft.Extensions.Logging;

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

    public async Task<string> GetLastUserMessage(Guid threadId)
    {
        var ThreadMessages = await _threadRepository.GetMessagesAsync(threadId);
        var lastUserMessage = ThreadMessages.LastOrDefault(m => m.Author.Role == Role.User);
        return lastUserMessage?.Text ?? string.Empty;
    }
}
