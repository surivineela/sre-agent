// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Logging;
using Azure.Core;
using Azure.Search.Documents;
using Azure.Search.Documents.Models;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SearchDocument = Agent.Core.Models.Api.v1.SearchDocument;

namespace Agent.Core.Helpers;
public class SearchHelper
{
    private readonly ILogger<SearchHelper> _logger;
    private readonly ISearchEndpointService _searchEndpointService;
    private readonly SearchEndpointSettings _searchEndpointSettings;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly SearchSettings _searchSettings;
    private readonly IAuthenticationService _authService;


    private const int MaxContentLengthForLLM = 2000;
    private const int MAX_RESULTS_TO_FETCH = 15;

    public SearchHelper(
            ILogger<SearchHelper> logger,
            ISearchEndpointService searchEndpointService,
            AzureSettings azureSettings,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            IHostEnvironment hostEnvironment,
            IOptions<SearchSettings> settings,
            IAuthenticationService authService)
    {
        _logger = logger;
        _searchEndpointService = searchEndpointService;
        _searchEndpointSettings = azureSettings.SearchEndpoint;
        _embeddingGenerator = embeddingGenerator;
        _hostEnvironment = hostEnvironment;
        _searchSettings = settings.Value;
        _authService = authService;
    }

    public async Task<List<SearchDocument>> SearchAsync(string searchText)
    {
        if (string.IsNullOrEmpty(_searchEndpointSettings.SearchEndpointUrl) || !_searchEndpointSettings.EnableDocumentRetrieval)
        {
            _logger.LogInternalInformation("Search endpoint URL is empty or document retrieval is disabled. Returning empty results.");
            return new List<SearchDocument>();
        }

        try
        {

            if (_hostEnvironment.IsDevelopment())
            {
                _logger.LogInternalInformation($"Development Environment. Searching with query: '{searchText}'");
                var searchClient = GetSearchClient();

                var options = new SearchOptions
                {
                    IncludeTotalCount = true,
                    QueryType = SearchQueryType.Full,
                    Size = MAX_RESULTS_TO_FETCH
                };

                var response = await searchClient.SearchAsync<SearchArticle>(searchText, options, CancellationToken.None);

                _logger.LogInternalInformation($"Search returned {response.Value.TotalCount} results from direct SearchClient.");

                var srchResults = response.Value.GetResults()
                                .Select(x => new SearchDocument(
                                    Id: x.Document.Id,
                                    Content: x.Document.Content ?? string.Empty,
                                    Title: x.Document.Title ?? string.Empty,
                                    Url: x.Document.Url ?? string.Empty
                                ))
                                .ToList();

                return srchResults;
            }

            _logger.LogInternalInformation($"Querying search endpoint service with query: '{searchText}'");

            float[]? vector = null;
            if (_searchEndpointSettings.EnableVectorSearch)
            {
                vector = await DocumentRetrieval.GenerateSearchVector(_embeddingGenerator, searchText, _logger);
            }

            var results = await _searchEndpointService.SearchDocumentsAsync(searchText, vector);

            _logger.LogInternalInformation($"Search returned {results.Count} results from ISearchEndpointService.");

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

    // This method remains private within SearchHelper as it's an internal detail of how SearchHelper gets its client.
    private SearchClient GetSearchClient()
    {
        // Using _searchSettings.SearchServiceEndpoint here
        if (string.IsNullOrEmpty(_searchSettings.SearchServiceEndpoint) || string.IsNullOrEmpty(_searchSettings.IndexName))
        {
            _logger.LogInternalError($"Search service endpoint or index name cannot be empty in SearchHelper.GetSearchClient().");
            throw new InvalidOperationException("Search service endpoint and index name must be configured.");
        }

        TokenCredential credential = _authService.GetSearchPluginCredential();

        // Create a new client for this specific index using Managed Identity or API Key
        return new SearchClient(
             new Uri(_searchSettings.SearchServiceEndpoint),
             _searchSettings.IndexName,
             credential);
    }

}
