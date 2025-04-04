// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Communication;

public class CosmosThreadOrchestrationManager : IThreadOrchestrationManager
{
    private readonly IThreadOrchestrationMappingRepository _repository;
    private readonly ILogger<CosmosThreadOrchestrationManager> _logger;

    public CosmosThreadOrchestrationManager(
            IThreadOrchestrationMappingRepository repository,
            ILogger<CosmosThreadOrchestrationManager> logger)
    {
        _repository = repository;
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
            _logger.LogError(ex, "Error getting mappings for thread ID {ThreadId}", threadId);
            return Enumerable.Empty<ThreadOrchestrationMapping>();
        }
    }

    public async Task AddMappingAsync(ThreadOrchestrationMapping mapping)
    {
        if (string.IsNullOrEmpty(mapping.ThreadId) || string.IsNullOrEmpty(mapping.OrchestrationInstanceId))
        {
            _logger.LogWarning("Cannot add mapping with null or empty ThreadId or OrchestrationInstanceId");
            return;
        }

        try
        {
            await _repository.AddThreadMappingAsync(mapping);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding mapping for thread ID {ThreadId} and orchestration instance ID {InstanceId}",
                mapping.ThreadId, mapping.OrchestrationInstanceId);
        }
    }

    public async Task AddMappingAsync(string threadId, string instanceId)
    {
        if (string.IsNullOrEmpty(threadId) || string.IsNullOrEmpty(threadId))
        {
            _logger.LogWarning("Cannot add mapping with null or empty ThreadId or OrchestrationInstanceId");
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

    public async Task RemoveMappingAsync(string threadId)
    {
        try
        {
            await _repository.RemoveThreadMappingAsync(threadId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing mappings for thread ID {ThreadId}", threadId);
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
            _logger.LogError(ex, "Error removing mapping for thread ID {ThreadId} and orchestration instance ID {InstanceId}",
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
            _logger.LogError(ex, "Error getting all mappings");
            return Enumerable.Empty<ThreadOrchestrationMapping>();
        }
    }
}
