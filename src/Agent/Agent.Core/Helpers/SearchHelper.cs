// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Framework;
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
    private readonly IChatClientProvider _chatClientProvider;
    private readonly Tracer _tracer;

    private const int MaxContentLengthForLLM = 2000;

    public SearchHelper(
            ILogger<SearchHelper> logger,
            ISearchEndpointService searchEndpointService,
            AzureSettings azureSettings,
            IChatClientProvider chatClientProvider,
            Tracer tracer)
    {
        _logger = logger;
        _searchEndpointService = searchEndpointService;
        _searchEndpointSettings = azureSettings.SearchEndpoint;
        _chatClientProvider = chatClientProvider;
        _tracer = tracer;
    }

    public async Task<List<SearchDocument>> SearchAsync(string searchText,
                                                        string documentType,
                                                        bool retrieveFullDocument = false,
                                                        TelemetrySpan? parentSpan = null,
                                                        string? threadId = null)
    {
        if (string.IsNullOrEmpty(_searchEndpointSettings.SearchEndpointUrl) || !_searchEndpointSettings.EnableDocumentRetrieval)
        {
            _logger.LogInternalInformation("Search endpoint URL is empty or document retrieval is disabled. Returning empty results.");
            return new List<SearchDocument>();
        }

        TelemetrySpan? searchSpan = null;
        TelemetrySpan? span = null;
        if (parentSpan != null)
        {
            searchSpan = _tracer.StartActiveSpan("retrieval_search_documents", SpanKind.Internal, parentSpan);
            searchSpan.SetAttribute(TraceAttribute.ThreadId, threadId);
            searchSpan.SetAttribute(TraceAttribute.OperationName, "retrieval.search.documents");
        }

        try
        {
            float[]? vector = null;
            var searchType = SearchType.FullText;

            if (_searchEndpointSettings.EnableVectorSearch)
            {
                if (searchSpan != null)
                {
                    span = _tracer.StartActiveSpan("generate_search_vector", SpanKind.Client, searchSpan);
                    span.SetAttribute(TraceAttribute.ThreadId, threadId);
                    span.SetAttribute(TraceAttribute.OperationName, "generate.search.vector");
                }
                searchType = SearchType.Hybrid;
                _logger.LogInternalInformation($"Generating embedding for '{searchText}'");
                vector = await DocumentRetrieval.GenerateSearchVector(_chatClientProvider.EmbeddingModel, searchText, _searchEndpointSettings.VectorDimensions, _logger);
                span?.End();
                span = null;
            }

            _logger.LogInternalInformation($"Querying search endpoint service with query: '{searchText}'");

            if (searchSpan != null)
            {
                span = _tracer.StartActiveSpan("query_search_endpoint", SpanKind.Client, searchSpan);
                span.SetAttribute(TraceAttribute.ThreadId, threadId);
                span.SetAttribute(TraceAttribute.OperationName, "query.search.endpoint");
            }
            var results = await _searchEndpointService.SearchDocumentsAsync(searchText,
                                                                            documentType,
                                                                            vector,
                                                                            searchType,
                                                                            retrieveFullDocument: retrieveFullDocument);
            span?.End();
            span = null;

            _logger.LogInternalInformation($"Search returned {results.Count} results from search endpoint.");

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

            searchSpan?.SetAttribute("search.results.count", optimizedResults.Count.ToString());
            searchSpan?.SetAttribute("search.results", JsonSerializer.Serialize(optimizedResults));

            if (searchSpan != null)
            {
                span = _tracer.StartSpan("llm_rerank", SpanKind.Internal, span);
                span.SetAttribute(TraceAttribute.ThreadId, threadId);
                span.SetAttribute(TraceAttribute.OperationName, "retrieval.llm.rerank");
            }
            var reranked = await DocumentRetrieval.RerankWithLLM(_chatClientProvider.DefaultModel, searchText, optimizedResults, _logger);
            span?.End();
            span = null;

            var rerankedDocuments = reranked.Select(id => optimizedResults.FirstOrDefault(doc => doc.Id == id)).Where(doc => doc != null).Take(3).Cast<SearchDocument>().ToList();
            searchSpan?.SetAttribute("search.reranked", JsonSerializer.Serialize(rerankedDocuments));

            return rerankedDocuments;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogInternalError(ex, $"Request to search endpoint failed.");
            searchSpan?.SetAttribute("search.error", ex.Message);
            // Return empty list on network error
            return new List<SearchDocument>();
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"An unexpected error occurred during search with query '{searchText}'");
            searchSpan?.SetAttribute("search.error", ex.Message);
            throw;
        }
        finally
        {
            span?.End();
            searchSpan?.End();
        }
    }

    public string FormatSearchResult(IList<SearchDocument> searchResults)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"Here are some relevant documents that can be referenced for user's query. Identify user's intent and reflect on these documents. If the documents are not helpful, you can ignore them:");
        sb.AppendLine("<Documents>");
        foreach (var doc in searchResults)
        {
            if (doc == null)
            {
                continue;
            }
            sb.AppendLine($"Title: {doc.Title}");
            sb.AppendLine($"Content: {doc.Content}");
            if (!string.IsNullOrEmpty(doc.Url))
            {
                sb.AppendLine($"Reference url: {doc.Url}");
            }
            sb.AppendLine();
            sb.AppendLine();
        }
        sb.AppendLine("</Documents>");
        return sb.ToString();
    }
}
