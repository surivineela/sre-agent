// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Logging;
using Agent.Plugins.Helpers;
using Agent.Plugins.Interface;
using Azure.Core;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Agent.Plugins.Implementation
{
    public class SearchPlugin : ISearchPlugin
    {
        private readonly ILogger<SearchPlugin> _logger;
        private readonly ISearchEndpointService _searchEndpointService;
        private readonly SearchEndpointSettings _searchEndpointSettings;
        private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;

        private const int MaxContentLengthForLLM = 2000;

        public SearchPlugin(
            ILogger<SearchPlugin> logger,
            ISearchEndpointService searchEndpointService,
            AzureSettings azureSettings,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator)
        {
            _logger = logger;
            _searchEndpointService = searchEndpointService;
            _searchEndpointSettings = azureSettings.SearchEndpoint;
            _embeddingGenerator = embeddingGenerator;
        }

        public async Task<List<SearchDocument>> SearchAsync(string searchText)
        {
            if (string.IsNullOrEmpty(_searchEndpointSettings.SearchEndpointUrl) || !_searchEndpointSettings.EnableDocumentRetrieval)
            {
                return new List<SearchDocument>();
            }

            try
            {
                return await KernelFunctionHelpers.TryAction(
                    nameof(SearchPlugin),
                    async () =>
                    {
                        _logger.LogInternalInformation($"Querying search endpoint with query: '{searchText}'");
                        float[]? vector = null;
                        if (_searchEndpointSettings.EnableVectorSearch)
                        {
                            vector = await DocumentRetrieval.GenerateSearchVector(_embeddingGenerator, searchText, _logger);
                        }

                        var results = await _searchEndpointService.SearchDocumentsAsync(searchText, vector);

                        _logger.LogInternalInformation($"Search returned {results.Count} results");

                        // Before returning results, process them, this is to avoid context length exceeded error in ProcessUserMessageAsync in metaagent
                        var optimizedResults = new List<SearchDocument>();
                        foreach (var result in results)
                        {
                            string summarizedContent = result.Content;

                            if (summarizedContent.Length > MaxContentLengthForLLM)
                            {
                                summarizedContent = summarizedContent.Substring(0, MaxContentLengthForLLM) + "...";
                            }

                            optimizedResults.Add(result with
                            {
                                Content = summarizedContent,
                            });
                        }
                        return optimizedResults;
                    },
                    _logger
                );
            }
            catch (HttpRequestException ex)
            {
                _logger.LogInternalError(ex, $"Request to search endpoint failed.");
                // Return empty list on network error
                return new List<SearchDocument>();
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"An unexpected error occurred during search with query '{searchText}'");
                throw;
            }
        }
    }
}
