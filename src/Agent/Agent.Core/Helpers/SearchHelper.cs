// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Logging;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Trace;
using SearchDocument = Agent.Core.Models.Api.v1.SearchDocument;

namespace Agent.Core.Helpers;

public class SearchHelper
{
    private readonly ILogger<SearchHelper> _logger;
    private readonly ISearchEndpointService _searchEndpointService;
    private readonly SearchEndpointSettings _searchEndpointSettings;
    private readonly IEmbeddingGenerator<string, Embedding<float>> _embeddingGenerator;
    private readonly Tracer _tracer;

    private const int MaxContentLengthForLLM = 2000;

    public SearchHelper(
            ILogger<SearchHelper> logger,
            ISearchEndpointService searchEndpointService,
            AzureSettings azureSettings,
            IEmbeddingGenerator<string, Embedding<float>> embeddingGenerator,
            Tracer tracer)
    {
        _logger = logger;
        _searchEndpointService = searchEndpointService;
        _searchEndpointSettings = azureSettings.SearchEndpoint;
        _embeddingGenerator = embeddingGenerator;
        _tracer = tracer;
    }

    public async Task<List<SearchDocument>> SearchAsync(string searchText, TelemetrySpan? parentSpan = null, string? threadId = null)
    {
        if (string.IsNullOrEmpty(_searchEndpointSettings.SearchEndpointUrl) || !_searchEndpointSettings.EnableDocumentRetrieval)
        {
            _logger.LogInternalInformation("Search endpoint URL is empty or document retrieval is disabled. Returning empty results.");
            return new List<SearchDocument>();
        }

        try
        {
            float[]? vector = null;
            var searchType = SearchType.FullText;
            TelemetrySpan? span = null;
            if (_searchEndpointSettings.EnableVectorSearch)
            {
                if (parentSpan != null)
                {
                    span = _tracer.StartActiveSpan("generate_search_vector", SpanKind.Client, parentSpan);
                    span.SetAttribute(TraceAttribute.ThreadId, threadId);
                    span.SetAttribute(TraceAttribute.OperationName, "generate.search.vector");
                }
                searchType = SearchType.Hybrid;
                _logger.LogInternalInformation($"Generating embedding for '{searchText}'");
                vector = await DocumentRetrieval.GenerateSearchVector(_embeddingGenerator, searchText, _searchEndpointSettings.VectorDimensions, _logger);
                span?.End();
            }

            _logger.LogInternalInformation($"Querying search endpoint service with query: '{searchText}'");

            if (parentSpan != null)
            {
                span = _tracer.StartActiveSpan("query_search_endpoint", SpanKind.Client, parentSpan);
                span.SetAttribute(TraceAttribute.ThreadId, threadId);
                span.SetAttribute(TraceAttribute.OperationName, "query.search.endpoint");
            }
            var results = await _searchEndpointService.SearchDocumentsAsync(searchText, vector, searchType);
            span?.End();

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
}
