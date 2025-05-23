// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Models.Api.v1;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using Agent.Logging;
using Azure.Core;
using Agent.Core.Interfaces;

namespace Agent.Runtime.Services;

public class SearchEndpointService : ISearchEndpointService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly SearchEndpointSettings _searchEndpointSettings;
    private readonly ILogger<SearchEndpointService> _logger;
    private readonly IAuthenticationService _authenticationService;

    public SearchEndpointService(
        ILogger<SearchEndpointService> logger,
        IHttpClientFactory httpClientFactory,
        IOptions<AzureSettings> azureSettings,
        IAuthenticationService authenticationService)
    {
        _httpClientFactory = httpClientFactory;
        _searchEndpointSettings = azureSettings.Value.SearchEndpoint;
        _logger = logger;
        _authenticationService = authenticationService;
    }

    public async Task<IReadOnlyList<SearchDocument>> GetAllDocumentsAsync()
    {
        _logger.LogInternalInformation("Getting all documents from search endpoint");
        return await SendRequestAsync<List<SearchDocument>>(
            HttpMethod.Get,
            "/search/documents");
    }

    public async Task<IReadOnlyList<SearchDocument>> SearchDocumentsAsync(string term)
    {
        _logger.LogInternalInformation($"Getting documents with term {term} from search endpoint");
        var path = $"/search?term={Uri.EscapeDataString(term)}";
        return await SendRequestAsync<List<SearchDocument>>(
            HttpMethod.Get,
            path);
    }

    private async Task<T> SendRequestAsync<T>(HttpMethod method, string path)
    {
        string baseUrl = _searchEndpointSettings.SearchEndpointUrl ?? throw new InvalidOperationException("SearchEndpoint:Url is not set");
        string fullUrl = $"{baseUrl.TrimEnd('/')}{path}";

        _logger.LogInternalInformation($"Sending {method} request to search endpoint {fullUrl}");

        try
        {
            var client = _httpClientFactory.CreateClient();
            var request = new HttpRequestMessage(method, fullUrl);

            // TODO: cache the token, then only renew it if it has expired or once on a 401
            var tokenRequestContext = new TokenRequestContext(new[] { "https://azuresre.ai/.default" });

            var cred = _authenticationService.GetAzureOpenAICredential();
            var accessToken = await cred.GetTokenAsync(tokenRequestContext, CancellationToken.None);

            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);

            using var response = await client.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                string errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogInternalError($"Search endpoint returned error: {(int)response.StatusCode} {response.ReasonPhrase}, Content: {errorContent}", null);
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
