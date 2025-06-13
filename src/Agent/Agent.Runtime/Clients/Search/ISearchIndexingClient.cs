// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure;
using Azure.Core;
using Azure.Search.Documents.Indexes.Models;

namespace Agent.Runtime.Clients.Search;
public interface ISearchIndexingClient
{
    Task<Response<SearchIndexerDataSourceConnection>> CreateOrUpdateBlobDataSourceAsync(string name, string containerName, string rootPath, ResourceIdentifier blobStorageResourceId, ResourceIdentifier managedIdentityResourceId);
    Task<Response<SearchIndex>> CreateOrUpdateIndexAsync(SearchIndex searchIndex, bool recreateOnError = false);
    Task<Response<SearchIndexer>> CreateOrUpdateIndexerAsync(SearchIndexer indexerDefinition);
    Task<Response<SearchIndexerSkillset>> CreateOrUpdateSkillsetAsync(SearchIndexerSkillset skillsetDefinition);
}
