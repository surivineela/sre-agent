// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Threading;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Models;
using Agent.Logging;
using Agent.Plugins.Interface;
using Azure;
using Azure.Identity;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation
{
    /// <summary>
    /// Implementation of IAzureSearchPlugin that provides TSG content retrieval
    /// using Azure Cognitive Search
    /// </summary>
    public class AzureSearchPlugin : IAzureSearchPlugin
    {
        private readonly ILogger<AzureSearchPlugin> _logger;
        private readonly AzureSearchSettings _searchSettings;
        private readonly TsgCrawlerSettings _tsgCrawlerSettings;

        public AzureSearchPlugin(ILogger<AzureSearchPlugin> logger, ExternalSettings externalSettings)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _searchSettings = externalSettings.AzureSearch ?? throw new ArgumentNullException(nameof(externalSettings.AzureSearch));
            _tsgCrawlerSettings = externalSettings.TsgCrawler ?? throw new ArgumentNullException(nameof(externalSettings.TsgCrawler));
        }

        /// <summary>
        /// Retrieves TSG content based on search text
        /// </summary>
        /// <param name="searchText">Text to search for in the TSG content</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>Search result containing TSG content</returns>
        public async Task<SearchResult> GetTsgContent(string searchText, CancellationToken cancellationToken = default)
        {
            _logger.LogInternalInformation($"Retrieving TSG content for search text: {searchText}");

            try
            {
                if (string.IsNullOrWhiteSpace(searchText))
                {
                    throw new ArgumentException("Search text cannot be empty", nameof(searchText));
                }

                // Use TsgCrawlerSettings.AiSearchSettings as primary search configuration
                var searchIndex = _tsgCrawlerSettings.AiSearchSettings.SearchIndexes.FirstOrDefault()?.IndexName;
                if (string.IsNullOrWhiteSpace(searchIndex))
                {
                    _logger.LogInternalWarning("TSG search index not found in TsgCrawlerSettings, falling back to default settings");
                    searchIndex = _searchSettings.SearchIndexes.FirstOrDefault()?.IndexName;
                    
                    if (string.IsNullOrWhiteSpace(searchIndex))
                    {
                        throw new InvalidOperationException("TSG search index not found in any settings");
                    }
                }

                var searchClient = GetSearchClientFromTsgSettings(searchIndex);
                _logger.LogInternalInformation($"Searching TSG content with index: {searchIndex}, query: {searchText}");

                var searchOptions = new SearchOptions
                {
                    IncludeTotalCount = true,
                    Size = 1 // Only retrieve the top result
                };

                // Configure search options based on index settings from TsgCrawlerSettings
                var searchIndexSettings = _tsgCrawlerSettings.AiSearchSettings.SearchIndexes
                    .FirstOrDefault(index => index.IndexName.Equals(searchIndex, StringComparison.OrdinalIgnoreCase));

                if (searchIndexSettings != null)
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

                    foreach (var field in searchIndexSettings.FieldsToSelect)
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
                    searchOptions.QueryType = SearchQueryType.Full;
                }

                var searchResults = await searchClient.SearchAsync<SearchResult>(searchText, searchOptions, cancellationToken);
                
                var resultList = searchResults.Value.GetResults().ToList();
                if (resultList.Count == 0)
                {
                    _logger.LogInternalWarning("No TSG content found for the query");
                    return new SearchResult();
                }

                return resultList[0].Document;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                _logger.LogInternalError(ex, $"Error retrieving TSG content for search text: {searchText}");
                throw;
            }
        }

        private SearchClient GetSearchClientFromTsgSettings(string searchIndex)
        {
            var settings = _tsgCrawlerSettings.AiSearchSettings;
            
            if (!string.IsNullOrWhiteSpace(settings.SearchApiKeyOverride))
            {
                var credential = new AzureKeyCredential(settings.SearchApiKeyOverride);
                return new SearchClient(new Uri(settings.SearchServiceUri), searchIndex, credential);
            }
            else if (!string.IsNullOrWhiteSpace(settings.UserAssignedMIClientId))
            {
                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = settings.UserAssignedMIClientId
                });
                return new SearchClient(new Uri(settings.SearchServiceUri), searchIndex, credential);
            }
            else
            {
                // Fall back to the regular search client
                return GetSearchClient(searchIndex);
            }
        }

        private SearchClient GetSearchClient(string searchIndex)
        {
            if (!string.IsNullOrWhiteSpace(_searchSettings.SearchApiKeyOverride))
            {
                var credential = new AzureKeyCredential(_searchSettings.SearchApiKeyOverride);
                return new SearchClient(new Uri(_searchSettings.SearchServiceUri), searchIndex, credential);
            }
            else if (!string.IsNullOrWhiteSpace(_searchSettings.UserAssignedMIClientId))
            {
                var credential = new DefaultAzureCredential(new DefaultAzureCredentialOptions
                {
                    ManagedIdentityClientId = _searchSettings.UserAssignedMIClientId
                });
                return new SearchClient(new Uri(_searchSettings.SearchServiceUri), searchIndex, credential);
            }
            else
            {
                var missingConfig = IsDevelopment() ? "SearchApiKeyOverride" : "UserAssignedMIClientId";
                throw new ArgumentException($"Configuration for {missingConfig} is missing or invalid.");
            }
        }

        private static bool IsDevelopment()
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        }
    }
}
