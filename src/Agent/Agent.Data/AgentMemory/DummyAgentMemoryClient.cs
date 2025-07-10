// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.AgentMemory;

// A dummy implementation of IAgentMemoryClient to make DI easier when AgentMemorySettings.Enabled feature flag is not turned on.
public class DummyAgentMemoryClient() : IAgentMemoryClient
{
    public Task<bool> UploadDocumentAsync(string fileName, Stream documentStream)
    {
        // Dummy implementation for testing purposes
        return Task.FromResult(true);
    }
    public Task SetupIndexerAsync()
    {
        // Dummy implementation for testing purposes
        return Task.CompletedTask;
    }

    public Task RunIndexerAsync()
    {
        // Dummy implementation for testing purposes
        return Task.CompletedTask;
    }

    public Task<IList<SearchDocumentResult>> SearchCustomerDocumentsAsync(
        string query,
        uint k = 5,
        float? vectorSimilarityThreshold = null,
        bool exhaustiveKnn = false,
        string? filter = null,
        bool enableHybridSearch = false,
        CancellationToken cancellationToken = default)
    {
        // Dummy implementation for testing purposes
        return Task.FromResult<IList<SearchDocumentResult>>(new List<SearchDocumentResult>());
    }

    public Task<IList<SearchDocumentResult>> SearchTrajectoriesAsync(string query, uint k = 5, float? vectorSimilarityThreshold = null, bool exhaustiveKnn = false, string? filter = null, bool enableHybridSearch = false, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IList<SearchDocumentResult>>([]);
    }
}
