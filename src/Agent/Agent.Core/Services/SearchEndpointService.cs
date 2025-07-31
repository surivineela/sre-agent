// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.Logging;
using Agent.Core.Interfaces;
using System.Text;
using System.Text.Json.Serialization;

namespace Agent.Core.Services;

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum SearchType
{
    FullText,
    Vector,
    Hybrid
}

public class SearchRequest
{
    public const string TypeDocument = "document";
    public const string TypeRunbook = "runbook";
    public SearchType SearchType { get; set; } = SearchType.Hybrid;
    public string SearchText { get; set; } = string.Empty;
    public string DocumentType { get; set; } = TypeDocument;
    public float[]? Vector { get; set; }
    public int? Top { get; set; }
    public List<string>? Categories { get; set; }
    public bool RetrieveFullDocument { get; set; } = false;
}

public class SearchEndpointService : ISearchEndpointService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SearchEndpointSettings _searchEndpointSettings;
    private readonly ILogger<SearchEndpointService> _logger;
    private readonly int _defaultTop;
    public SearchEndpointService(
        ILogger<SearchEndpointService> logger,
        IHttpClientFactory httpClientFactory,
        AzureSettings azureSettings)
    {
        _httpClientFactory = httpClientFactory;
        _searchEndpointSettings = azureSettings.SearchEndpoint;
        _logger = logger;
        _defaultTop = _searchEndpointSettings.Top ?? 10;
    }

    public async Task<IReadOnlyList<SearchDocument>> GetAllDocumentsAsync()
    {
        _logger.LogInternalInformation("Getting all documents from search endpoint");
        return await SendRequestAsync<List<SearchDocument>>(
            HttpMethod.Get,
            "/search/documents");
    }

    public async Task<IReadOnlyList<SearchDocument>> SearchDocumentsAsync(string query,
                                                                        string documentType,
                                                                        float[]? vectors,
                                                                        SearchType searchType,
                                                                        int? top = null,
                                                                        bool retrieveFullDocument = false)
    {
        top = top ?? _defaultTop;
        _logger.LogInternalInformation($"Getting top {top} documents with term {query} from search endpoint");
        var path = $"/search";
        var searchRequest = new SearchRequest
        {
            SearchText = query,
            DocumentType = documentType,
            Top = top,
            SearchType = searchType,
            RetrieveFullDocument = retrieveFullDocument
        };

        if (vectors != null && vectors.Length > 0)
        {
            searchRequest.Vector = vectors;
        }

        return await SendRequestAsync<List<SearchDocument>>(
            HttpMethod.Post,
            path,
            searchRequest);
    }

    private async Task<T> SendRequestAsync<T>(HttpMethod method, string path, SearchRequest? searchRequest = null)
    {
        string baseUrl = _searchEndpointSettings.SearchEndpointUrl;
        string fullUrl = $"{baseUrl.TrimEnd('/')}{path}";

        _logger.LogInternalInformation($"Sending {method} request to search endpoint {fullUrl}");

        try
        {
            var client = _httpClientFactory.CreateClient(Constants.HttpClientForSearchEndpoint);
            var request = new HttpRequestMessage(method, fullUrl);

            if (searchRequest != null)
            {
                request.Content = new StringContent(JsonSerializer.Serialize(searchRequest), Encoding.UTF8, "application/json");
            }

            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogInternalError($"Search endpoint returned error: {(int)response.StatusCode} {response.ReasonPhrase}, Content: {errorContent}");
                response.EnsureSuccessStatusCode();
            }

            var content = await response.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            })!;
        }
        catch (Exception ex)
        {
            _logger.LogInternalError("An error occurred while sending request to search endpoint", ex);
            throw;
        }
    }
}
