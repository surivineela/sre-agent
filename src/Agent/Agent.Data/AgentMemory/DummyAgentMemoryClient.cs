// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.AgentMemory;

// A dummy implementation of IAgentMemoryClient to make DI easier when AgentMemorySettings.Enabled feature flag is not turned on.
public class DummyAgentMemoryClient() : IAgentMemoryClient
{
    public Task<bool> UploadDocumentAsync(string fileName, Stream documentStream)
    {
        return Task.FromResult(true);
    }

    public Task<bool> DeleteDocumentAsync(string fileName)
    {
        return Task.FromResult(true);
    }

    public Task SetupIndexerAsync()
    {
        return Task.CompletedTask;
    }

    public Task RunIndexerAsync()
    {
        return Task.CompletedTask;
    }

    public Task<IList<SearchDocumentResult>> SearchCustomerDocumentsAsync(SearchParams searchParams, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IList<SearchDocumentResult>>([]);
    }

    public Task<IList<SearchDocumentResult>> SearchTrajectoriesAsync(SearchParams searchOptions, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IList<SearchDocumentResult>>([]);
    }

    public Task<IList<SearchDocumentResult>> SearchUserMemoriesAsync(SearchParams searchOptions, CancellationToken cancellationToken = default)
    {
        return Task.FromResult<IList<SearchDocumentResult>>([]);
    }

    public Task<BlobListPage> ListFilesAsync(string? prefix = null, int? pageSize = null, string? continuationToken = null, CancellationToken cancellationToken = default)
    {
        // Dummy implementation returns empty list
        return Task.FromResult(new BlobListPage(Array.Empty<string>(), null));
    }
}
