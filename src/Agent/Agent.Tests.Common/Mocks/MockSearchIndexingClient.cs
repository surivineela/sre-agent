using Agent.Core.Clients.Search;
using Azure;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

namespace Agent.Tests.Common.Mocks;
internal class MockSearchIndexingClient : ISearchIndexingClient
{
    public async Task<Response<SearchIndexerDataSourceConnection>> CreateOrUpdateBlobDataSourceAsync(string name, string containerName, string rootPath, ResourceIdentifier blobStorageResourceId, ResourceIdentifier managedIdentityResourceId)
    {
        await Task.Yield();

        return new MockAzureHttpResponse<SearchIndexerDataSourceConnection>(
            new SearchIndexerDataSourceConnection(
                name,
                SearchIndexerDataSourceType.AzureBlob,
                blobStorageResourceId.ToString(),
                new SearchIndexerDataContainer(containerName)),
            new MockAzureHttpResponse(200));
    }

    public async Task<Response<SearchIndex>> CreateOrUpdateIndexAsync(SearchIndex searchIndex, bool recreateOnError = false)
    {
        await Task.Yield();

        return new MockAzureHttpResponse<SearchIndex>(
            searchIndex,
            new MockAzureHttpResponse(200));
    }

    public async Task<Response<SearchIndexer>> CreateOrUpdateIndexerAsync(SearchIndexer indexerDefinition)
    {
        await Task.Yield();
        return new MockAzureHttpResponse<SearchIndexer>(
            indexerDefinition,
            new MockAzureHttpResponse(200));
    }

    public async Task<Response<SearchIndexerSkillset>> CreateOrUpdateSkillsetAsync(SearchIndexerSkillset skillsetDefinition)
    {
        await Task.Yield();
        return new MockAzureHttpResponse<SearchIndexerSkillset>(
            skillsetDefinition,
            new MockAzureHttpResponse(200));
    }

    public Task RunIndexerAsync(string indexerName, CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }

    public Task<SearchResults<TResult>> SearchAsync<TResult>(string indexName, string searchText, SearchOptions searchOptions, CancellationToken cancellationToken = default)
    {
        throw new NotImplementedException();
    }
}
