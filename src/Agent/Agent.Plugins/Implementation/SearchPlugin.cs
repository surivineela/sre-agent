// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Logging;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Plugins.Implementation
{
    public class SearchPlugin : ISearchPlugin
    {
        private readonly ILogger<SearchPlugin> _logger;
        private readonly SearchClient _searchClient;
        private readonly SearchSettings _settings;
        private readonly IAuthenticationService _authService;

        private const int MAX_RESULTS_TO_FETCH = 20;

        public SearchPlugin(
            IOptions<SearchSettings> settings,
            ILogger<SearchPlugin> logger,
            IAuthenticationService authservice)
        {
            _logger = logger;
            _settings = settings.Value;
            _authService = authservice;

            ValidateSettings();
            GetSearchClientForIndex(_settings.DefaultIndexName ?? "default-index");
        }

        public async Task<List<SearchArticle>> SearchAsync(
            string searchIndex,
            string searchText,
            CancellationToken cancellationToken = default)
        {
            try
            {
                return await KernelFunctionHelpers.TryAction(
                    nameof(SearchPlugin),
                    async () =>
                    {
                        ValidateSettings();

                        var searchClient = GetSearchClientForIndex(searchIndex);

                        var options = new SearchOptions
                        {
                            IncludeTotalCount = true,
                            QueryType = SearchQueryType.Full,
                            Size = MAX_RESULTS_TO_FETCH
                        };

                        _logger.LogInternalInformation($"Searching index '{searchIndex}' with query: '{searchText}'");
                        var response = await searchClient.SearchAsync<SearchArticle>(searchText, options, cancellationToken);

                        _logger.LogInternalInformation($"Search returned {response.Value.TotalCount} results");

                        var results = response.Value.GetResults().Select(x => x.Document).ToList();

                        // return results;

                        // Before returning results, process them, this is to avoid context length exceeded error in ProcessUserMessageAsync in metaagent
                        var optimizedResults = new List<SearchArticle>();
                        foreach (var article in results)
                        {
                            string summarizedContent = article.Content;
                            const int MaxContentLengthForLLM = 500;
                            if (summarizedContent.Length > MaxContentLengthForLLM)
                            {
                                summarizedContent = summarizedContent.Substring(0, MaxContentLengthForLLM) + "...";
                            }

                            optimizedResults.Add(new SearchArticle
                            {
                                Title = article.Title,
                                Content = summarizedContent,
                                Url = article.Url,
                                Id = article.Id,
                                Tag = article.Tag,
                            });
                        }
                        return optimizedResults;
                    },
                    _logger
                );
            }
            catch (HttpRequestException ex)
            {
                _logger.LogInternalError(ex, $"Network error during search for index '{searchIndex}' with query '{searchText}': {ex.Message}. This could be due to a 'no such host' issue, DNS problems, or firewall restrictions. Ensure the search service URL is correct and accessible.");
                // Return empty list on network error
                return new List<SearchArticle>();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"An unexpected error occurred during search for index '{searchIndex}' with query '{searchText}': {ex.Message}");
                throw;
            }
        }

        private void ValidateSettings()
        {
            if (_settings == null || string.IsNullOrEmpty(_settings.SearchServiceEndpoint))
            {
                _logger.LogInternalError("Azure Search Settings are not configured.");
            }
        }

        private SearchClient GetSearchClientForIndex(string indexName)
        {
            if (string.IsNullOrEmpty(indexName))
            {
                _logger.LogInternalError($"Index name cannot be empty");
            }

            if (_settings.DefaultIndexName == indexName)
            {
                return _searchClient; // Return the default client if querying the default index
            }

            TokenCredential credential = _authService.GetSearchPluginCredential();

            // Create a new client for this specific index using Managed Identity
            return new SearchClient(
                 new Uri(_settings.SearchServiceEndpoint),
                 indexName,
                 credential);
        }
    }
}
