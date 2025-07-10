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

    public Task<bool> IndexContentAsync(BaseIndexableContent content)
    {
        _logger.LogInternalInformation($"DummySearchIndexService: IndexContentAsync called with BaseIndexableContent ID: {content.Id}");
        return Task.FromResult(true);
    }

    public Task<bool> IndexContentAsync(AgentMemory content)
    {
        _logger.LogInternalInformation($"DummySearchIndexService: IndexContentAsync called with AgentMemory ID: {content.Id}");
        return Task.FromResult(true);
    }
}
