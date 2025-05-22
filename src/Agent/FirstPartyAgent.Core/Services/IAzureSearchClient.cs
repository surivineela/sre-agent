// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Collections.Concurrent;
using System.Diagnostics;
using Agent.Core.Configuration;
using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using FirstPartyAgent.Core.Plugins.Implementation;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Services
{
    public interface IAzureSearchClient
    {
        Task<SearchResults<T>> SearchAsync<T>(
            string searchIndex,
            string searchText,
            Action<SearchOptions>? configureOptions = null,
            CancellationToken cancellationToken = default);
    }

    public class AzureSearchClient : IAzureSearchClient
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

        public async Task<SearchResults<T>> SearchAsync<T>(
    string searchIndex,
    string searchText,
    Action<SearchOptions>? configureOptions = null,
    CancellationToken cancellationToken = default)
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

            var searchIndexSettings = _azureSearchSettings.SearchIndexes
                .FirstOrDefault(index => index.IndexName.Equals(searchIndex, StringComparison.OrdinalIgnoreCase));

            // Default behavior from config
            if (!string.IsNullOrWhiteSpace(searchIndexSettings?.IndexName))
            {
                if (searchIndexSettings.SemanticSearchEnabled)
                {
                    searchOptions.SemanticSearch = new SemanticSearchOptions
                    {
                        SemanticConfigurationName = "default"
                    };
                    if (!searchIndexSettings.VectorSearchEnabled)
                    {
                        searchOptions.QueryType = SearchQueryType.Semantic;
                    }
                }

                if (searchIndexSettings.VectorSearchEnabled)
                {
                    var vectorSearch = new VectorSearchOptions();
                    var vectorQuery = new VectorizableTextQuery(searchText)
                    {
                        Exhaustive = true,
                        KNearestNeighborsCount = MAX_RESULTS_TO_FETCH
                    };

                    foreach (var field in searchIndexSettings.VectorFieldNames ?? Enumerable.Empty<string>())
                    {
                        if (!string.IsNullOrWhiteSpace(field))
                        {
                            vectorQuery.Fields.Add(field);
                        }
                    }

                    vectorSearch.Queries.Add(vectorQuery);
                    searchOptions.VectorSearch = vectorSearch;
                }

                foreach (var field in searchIndexSettings.FieldsToSelect ?? Enumerable.Empty<string>())
                {
                    if (!string.IsNullOrWhiteSpace(field))
                    {
                        searchOptions.Select.Add(field);
                    }
                }

                if (!searchIndexSettings.SemanticSearchEnabled && !searchIndexSettings.VectorSearchEnabled)
                {
                    searchOptions.QueryType = SearchQueryType.Full;
                }
            }
            else
            {
                // fallback defaults
                searchOptions.QueryType = SearchQueryType.Full;
                searchOptions.Size = MAX_RESULTS_TO_FETCH;
            }

            // 🔧 Allow caller to override anything
            configureOptions?.Invoke(searchOptions);
           
                // 📤 Execute query
                if (searchOptions.VectorSearch?.Queries?.Any() == true)
                {
                    return (await searchClient.SearchAsync<T>(searchOptions, cancellationToken)).Value;
                }
                else
                {
                    searchOptions.Size = searchOptions.Size ?? MAX_RESULTS_TO_FETCH;
                    return (await searchClient.SearchAsync<T>(searchText, searchOptions, cancellationToken)).Value;
                }
           
        }
    }
}
