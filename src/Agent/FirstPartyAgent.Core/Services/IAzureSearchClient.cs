// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Azure;
using Agent.Core.Configuration;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Configuration;
using System.Collections.Concurrent;

namespace FirstPartyAgent.Core.Services
{
    public interface IAzureSearchClient
    {
        Task<SearchResults<T>> SearchAsync<T>(string searchIndex, string searchText, CancellationToken cancellationToken = default);
    }

    public class AzureSearchClient: IAzureSearchClient
    {
        private const int MAX_RESULTS_TO_FETCH = 20;
        private readonly AzureSearchSettings _azureSearchSettings;
        private readonly ConcurrentDictionary<string, SearchClient> _searchClients = new(StringComparer.OrdinalIgnoreCase);

        public AzureSearchClient(AzureSearchSettings searchSettings)
        {
            _azureSearchSettings = searchSettings;
        }

        private static bool IsDevelopment()
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        }

        private SearchClient GetSearchClient(string searchIndex)
        {
            var client = _searchClients.GetOrAdd(searchIndex, index =>
            {
                if (!string.IsNullOrWhiteSpace(_azureSearchSettings.SearchApiKeyOverride))
                {
                    var credential = new AzureKeyCredential(_azureSearchSettings.SearchApiKeyOverride);
                    return new SearchClient(new Uri(_azureSearchSettings.SearchServiceUri), searchIndex, credential);
                }
                else if (!string.IsNullOrWhiteSpace(_azureSearchSettings.UserAssignedMIClientId))
                {
                    var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                    {
                        ManagedIdentityClientId = _azureSearchSettings.UserAssignedMIClientId
                    });
                    return new SearchClient(new Uri(_azureSearchSettings.SearchServiceUri), searchIndex, credential);
                }
                else
                {
                    var missingConfig = IsDevelopment() ? "SearchApiKeyOverride" : "UserAssignedMIClientId";
                    throw new ArgumentException($"Configuration for {missingConfig} is missing or invalid.");
                }
            });
            return client;
        }

        public async Task<SearchResults<T>> SearchAsync<T>(string searchIndex, string searchText, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(searchText))
            {
                throw new ArgumentException("Search text cannot be null or empty.", nameof(searchText));
            }

            if (string.IsNullOrWhiteSpace(searchIndex))
            {
                throw new ArgumentException("Search index cannot be null or empty.", nameof(searchIndex));
            }

            var searchClient = GetSearchClient(searchIndex);

            var searchOptions = new SearchOptions
            {
                IncludeTotalCount = true
            };

            var searchIndexSettings = _azureSearchSettings.SearchIndexes.FirstOrDefault(index => index.IndexName.Equals(searchIndex, StringComparison.OrdinalIgnoreCase));

            if (string.IsNullOrWhiteSpace(searchIndexSettings?.IndexName))
            {
                searchOptions.QueryType = SearchQueryType.Full;
                searchOptions.Size = MAX_RESULTS_TO_FETCH;

                var searchResults = await searchClient.SearchAsync<T>(searchText, searchOptions, cancellationToken);
                return searchResults.Value;
            }
            else
            {
                if (searchIndexSettings.SemanticSearchEnabled)
                {
                    if (!searchIndexSettings.VectorSearchEnabled)
                    {
                        searchOptions.QueryType = SearchQueryType.Semantic;
                    }

                    searchOptions.SemanticSearch = new SemanticSearchOptions
                    {
                        SemanticConfigurationName = "default",
                    };
                }

                if (searchIndexSettings.VectorSearchEnabled)
                {
                    searchOptions.VectorSearch = new VectorSearchOptions();

                    var vectorSearchQuery = new VectorizableTextQuery(searchText)
                    {
                        Exhaustive = true,
                        KNearestNeighborsCount = MAX_RESULTS_TO_FETCH
                    };

                    foreach (string vectorFieldName in searchIndexSettings.VectorFieldNames)
                    {
                        if (!string.IsNullOrWhiteSpace(vectorFieldName))
                        {
                            vectorSearchQuery.Fields.Add(vectorFieldName);
                        }
                    }

                    searchOptions.VectorSearch.Queries.Add(vectorSearchQuery);
                }

                foreach (string fieldName in searchIndexSettings.FieldsToSelect)
                {
                    if (!string.IsNullOrWhiteSpace(fieldName))
                    {
                        searchOptions.Select.Add(fieldName);
                    }
                }

                if (!searchIndexSettings.SemanticSearchEnabled && !searchIndexSettings.VectorSearchEnabled)
                {
                    searchOptions.QueryType = SearchQueryType.Full;
                }

                if (searchIndexSettings.VectorSearchEnabled)
                {
                    var searchResults = await searchClient.SearchAsync<T>(searchOptions, cancellationToken);
                    return searchResults.Value;
                }
                else
                {
                    searchOptions.Size = MAX_RESULTS_TO_FETCH;
                    var searchResults = await searchClient.SearchAsync<T>(searchText, searchOptions, cancellationToken);
                    return searchResults.Value;
                }
            }
        }
    }
}

