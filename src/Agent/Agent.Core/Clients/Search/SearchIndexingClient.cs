// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Logging;
using Azure;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Clients.Search;

public class SearchIndexingClient : ISearchIndexingClient
{
    private readonly SearchIndexClient _searchIndexClient;
    private readonly SearchIndexerClient _searchIndexerClient;
    private readonly ILogger<SearchIndexingClient> _logger;

    public SearchIndexingClient(IAuthenticationService authService, IndexingSettings indexingSettings, ILogger<SearchIndexingClient> logger)
    {
        TokenCredential credential = authService.GetSearchEndpointCredential();
        _searchIndexClient = new SearchIndexClient(new Uri(indexingSettings.SearchEndpoint), credential);
        _searchIndexerClient = new SearchIndexerClient(new Uri(indexingSettings.SearchEndpoint), credential);
        _logger = logger;
    }

    public async Task<Response<SearchIndex>> CreateOrUpdateIndexAsync(SearchIndex searchIndex, bool recreateOnError = false)
    {
        _logger.LogInternalInformation("Creating or updating search index {Name}", searchIndex.Name);

        try
        {
            return await _searchIndexClient.CreateOrUpdateIndexAsync(searchIndex);
        }
        catch (RequestFailedException ex)
        {
            if (ex.Status == 400 && ex.ErrorCode == "OperationNotAllowed" && recreateOnError)
            {
                await _searchIndexClient.DeleteIndexAsync(searchIndex.Name);
                return await _searchIndexClient.CreateIndexAsync(searchIndex);
            }

            throw;
        }
    }

    public async Task<Response<SearchIndexerSkillset>> CreateOrUpdateSkillsetAsync(SearchIndexerSkillset skillsetDefinition)
    {
        _logger.LogInternalInformation("Creating or updating search skillset {Name}", skillsetDefinition.Name);

        return await _searchIndexerClient.CreateOrUpdateSkillsetAsync(skillsetDefinition);
    }

    public async Task<Response<SearchIndexer>> CreateOrUpdateIndexerAsync(SearchIndexer indexerDefinition)
    {
        _logger.LogInternalInformation("Creating or updating search indexer {Name}", indexerDefinition.Name);

        return await _searchIndexerClient.CreateOrUpdateIndexerAsync(indexerDefinition);
    }

    public async Task<Response<SearchIndexerDataSourceConnection>> CreateOrUpdateBlobDataSourceAsync(
        string name,
        string containerName,
        string rootPath,
        ResourceIdentifier blobStorageResourceId,
        ResourceIdentifier managedIdentityResourceId)
    {
        _logger.LogInternalInformation("Creating or updating search indexing data source {Name}", name);

        // Create the connection string that uses the resource ID format
        var connectionString = $"ResourceId={blobStorageResourceId};";

        // Create the data source
        var dataSourceDefinition = new SearchIndexerDataSourceConnection(
            name,
            SearchIndexerDataSourceType.AzureBlob,
            connectionString,
            new SearchIndexerDataContainer(containerName) { Query = rootPath });

        dataSourceDefinition.IndexerPermissionOptions = []; // this can't be null, otherwise we get a 400 response.
        dataSourceDefinition.Identity = new SearchIndexerDataUserAssignedIdentity(managedIdentityResourceId);

        return await _searchIndexerClient.CreateOrUpdateDataSourceConnectionAsync(dataSourceDefinition);
    }

    public async Task<SearchResults<TResult>> SearchAsync<TResult>(string indexName, string searchText, SearchOptions searchOptions, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Performing search on index {IndexName} with query '{SearchText}'", indexName, searchText);
        SearchClient searchClient = _searchIndexClient.GetSearchClient(indexName);

        Response<SearchResults<TResult>> results = await searchClient.SearchAsync<TResult>(searchText, searchOptions, cancellationToken);

        return results.Value;
    }

    public async Task RunIndexerAsync(string indexerName, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Running search indexer {Name}", indexerName);

        await _searchIndexerClient.RunIndexerAsync(indexerName, cancellationToken);
    }
}
