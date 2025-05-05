// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Logging;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Communication;

public class CosmosThreadOrchestrationManager : IThreadOrchestrationManager
{
    private readonly IThreadOrchestrationMappingRepository _repository;
    private readonly IThreadRepository _threadRepository;
    private readonly ILogger<CosmosThreadOrchestrationManager> _logger;

    public CosmosThreadOrchestrationManager(
            IThreadOrchestrationMappingRepository repository,
            IThreadRepository threadRepository,
            ILogger<CosmosThreadOrchestrationManager> logger)
    {
        _repository = repository;
        _threadRepository = threadRepository;
        _logger = logger;
    }

    public async Task<IEnumerable<ThreadOrchestrationMapping>> GetMappingsByThreadIdAsync(string threadId)
    {
        try
        {
            return await _repository.GetMappingsByThreadIdAsync(threadId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting mappings for thread ID {ThreadId}", threadId);
            return Enumerable.Empty<ThreadOrchestrationMapping>();
        }
    }

    public async Task AddMappingAsync(ThreadOrchestrationMapping mapping)
    {
        if (string.IsNullOrEmpty(mapping.ThreadId) || string.IsNullOrEmpty(mapping.OrchestrationInstanceId))
        {
            _logger.LogInternalWarning("Cannot add mapping with null or empty ThreadId or OrchestrationInstanceId");
            return;
        }

        try
        {
            await UpdateThreadContextAsync(mapping.ThreadId, mapping.OrchestrationInstanceId);
            await _repository.AddThreadMappingAsync(mapping);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error adding mapping for thread ID {ThreadId} and orchestration instance ID {InstanceId}",
                mapping.ThreadId, mapping.OrchestrationInstanceId);
        }
    }

    public async Task AddMappingAsync(string threadId, string instanceId)
    {
        if (string.IsNullOrEmpty(threadId) || string.IsNullOrEmpty(threadId))
        {
            _logger.LogInternalWarning("Cannot add mapping with null or empty ThreadId or OrchestrationInstanceId");
            return;
        }

        await AddMappingAsync(new ThreadOrchestrationMapping(
               Id: $"mapping_{threadId}",
               ThreadId: threadId,
               OrchestrationInstanceId: instanceId,
               CreatedTimestamp: DateTime.UtcNow,
               ModifiedTimestamp: DateTime.UtcNow
               )
           );
    }

    private async Task UpdateThreadContextAsync(string threadIdStr, string orchestrationInstanceId)
    {
        if (!Guid.TryParse(threadIdStr, out var threadId))
        {
            _logger.LogInternalError($"Fail to parse threadID {threadIdStr} to valid Guid");
            return;
        }

        ThreadContext threadContext;
        try
        {
            threadContext = await _threadRepository.GetThreadContextAsync(threadId);
            // If the thread context is not found, create a new one with Meta agent type as default.
            threadContext ??= await _threadRepository.AddThreadContextAsync(new ThreadContext(threadId, AgentTypeEnum.Meta, true));
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to get or create thread context, threadId: {ThreadId}", threadIdStr);
            throw;
        }

        threadContext.OrchestrationState = new OrchestrationState
        {
            OrchestrationInstanceId = orchestrationInstanceId,
            StepCounter = 0,
            ReasoningState = ReasoningState.OrchestrationInitialized,
            StateMessage = "Orchestration initialized by agent",
            TimeStamp = DateTime.UtcNow
        };

        var serializedState = JsonSerializer.Serialize(threadContext.OrchestrationState);
        _logger.LogInternalInformation("Persisting thread context, threadId: {ThreadId}, state: {SerializedState}", threadIdStr, serializedState);

        try
        {
            threadContext = await _threadRepository.UpdateThreadContextAsync(threadContext);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to update thread context, threadId: {ThreadId}", threadIdStr);
            throw;
        }
        return;
    }

    public async Task RemoveMappingAsync(string threadId)
    {
        try
        {
            await _repository.RemoveThreadMappingAsync(threadId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error removing mappings for thread ID {ThreadId}", threadId);
        }
    }

    public async Task RemoveMappingAsync(string threadId, string orchestrationInstanceId)
    {
        try
        {
            await _repository.RemoveThreadMappingAsync(threadId, orchestrationInstanceId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error removing mapping for thread ID {ThreadId} and orchestration instance ID {InstanceId}",
                threadId, orchestrationInstanceId);
        }
    }

    public async Task<IEnumerable<ThreadOrchestrationMapping>> GetAllMappingsAsync()
    {
        try
        {
            return await _repository.GetAllThreadMappingsAsync();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error getting all mappings");
            return Enumerable.Empty<ThreadOrchestrationMapping>();
        }
    }
}
