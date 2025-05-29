// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Models;
using Agent.Logging;
using Agent.Plugins.Definitions;
using Agent.Plugins.Helpers;
using Azure;
using Azure.Core;
using Azure.Identity;
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

        public SearchPlugin(
            IOptions<SearchSettings> settings,
            ILogger<SearchPlugin> logger)
        {
            _logger = logger;
            _settings = settings.Value;

            ValidateSettings();
            GetSearchClientForIndex(_settings.DefaultIndexName ?? "default-index");
        }

        public async Task<List<SearchArticle>> SearchAsync(
            string searchIndex,
            string searchText,
            CancellationToken cancellationToken = default)
        {
            return await KernelFunctionHelpers.TryAction(
                nameof(SearchPlugin),
                async () =>
                {
                    ValidateSettings();

                    var searchClient = GetSearchClientForIndex(searchIndex);

                    var options = new SearchOptions
                    {
                        IncludeTotalCount = true
                    };

                    _logger.LogInternalInformation($"Searching index '{searchIndex}' with query: '{searchText}'");
                    var response = await searchClient.SearchAsync<SearchArticle>(searchText, options, cancellationToken);

                    _logger.LogInternalInformation($"Search returned {response.Value.TotalCount} results");

                    return response.Value.GetResults().Select(x => x.Document).ToList();
                },
                _logger
            );
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

            // Create a token credential using DefaultAzureCredential
            TokenCredential credential = new DefaultAzureCredential();

            // Create a new client for this specific index using Managed Identity
            return new SearchClient(
                new Uri($"https://{_settings.SearchServiceEndpoint}.search.windows.net/"),
                indexName,
                credential);
        }
    }
}
