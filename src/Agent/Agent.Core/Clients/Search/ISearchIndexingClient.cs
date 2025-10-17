// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.DataConnectors;
using Azure;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Indexes.Models;
using Azure.Search.Documents.Models;

namespace Agent.Core.Clients.Search;

public interface ISearchIndexingClient
{
    Task<Response<SearchIndexerDataSourceConnection>> CreateOrUpdateBlobDataSourceAsync(string name, string containerName, string rootPath, ResourceIdentifier blobStorageResourceId, ResourceIdentifier managedIdentityResourceId);
    Task<Response<SearchIndex>> CreateOrUpdateIndexAsync(SearchIndex searchIndex);
    Task<Response<SearchIndexer>> CreateOrUpdateIndexerAsync(SearchIndexer indexerDefinition);
    Task<Response<SearchIndexerSkillset>> CreateOrUpdateSkillsetAsync(SearchIndexerSkillset skillsetDefinition);
    Task<SearchResults<TResult>> SearchAsync<TResult>(string indexName, string searchText, SearchOptions searchOptions, CancellationToken cancellationToken = default);
    Task RunIndexerAsync(string indexerName, CancellationToken cancellationToken = default);
    Task<Response<IndexDocumentsResult>> DeleteDocumentsAsync(string indexName, IEnumerable<DataConnectorIndexDocument> documents, CancellationToken cancellationToken = default);
}
