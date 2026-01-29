// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;

namespace Agent.Data.AgentMemory;

public class DummySearchIndexService : ISearchIndexService
{
    private readonly ILogger<DummySearchIndexService> _logger;

    public DummySearchIndexService(ILogger<DummySearchIndexService> logger)
    {
        _logger = logger;
    }

    public Task CreateOrUpdateIndexAsync()
    {
        _logger.LogInternalInformation("DummySearchIndexService: CreateOrUpdateIndexAsync called");
        return Task.CompletedTask;
    }

    public Task<bool> DeleteContentsAsync(List<AgentMemory> memories)
    {
        return Task.FromResult(true);
    }

    public Task DeleteIndexIfExistsAsync()
    {
        return Task.CompletedTask;
    }

    public Task<bool> IndexContentAsync(AgentMemory content)
    {
        _logger.LogInternalInformation($"DummySearchIndexService: IndexContentAsync called with AgentMemory ID: {content.Id}");
        return Task.FromResult(true);
    }

    public Task<bool> UpsertTrajectoryAsync(AgentMemory trajectory)
    {
        _logger.LogInternalInformation($"DummySearchIndexService: UpsertTrajectoryAsync called with trajectory ID: {trajectory.Id}");
        return Task.FromResult(true);
    }

    public Task<AgentMemory?> GetTrajectoryByIdAsync(string trajectoryId)
    {
        _logger.LogInternalInformation($"DummySearchIndexService: GetTrajectoryByIdAsync called with trajectory ID: {trajectoryId}");
        return Task.FromResult<AgentMemory?>(null);
    }
}
