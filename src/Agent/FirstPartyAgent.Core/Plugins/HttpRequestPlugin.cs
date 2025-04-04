// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using System.ComponentModel;

namespace FirstPartyAgent.Core.Plugins
{
    public class HttpRequestPlugin
    {
        private readonly ILogger<HttpRequestPlugin> _logger;
        private readonly ITeamsClient _teamsClient;
        private static readonly HttpClient _httpClient = new HttpClient();
        public HttpRequestPlugin(ILogger<HttpRequestPlugin> logger, ITeamsClient teamsClient)
        {
            _logger = logger;
            _teamsClient = teamsClient;
        }

        [KernelFunction("make_http_get_request")]
        [Description("Make Http GET request to a URL. Takes in the requestUrl and returns the HTTP Status code.")]
        public async Task<string> MakeHttpGetRequest([Description("URL to make HTTP GET request to")] string requestUrl, Kernel kernel)
        {
            try
            {
                var logMessage = $"[make_http_get_request][{DateTime.UtcNow}] Invoked with requestUrl {requestUrl}";
                await kernel.LogInformation(logMessage, _logger, _teamsClient);
                var result = await _httpClient.GetAsync(requestUrl);
                var messageResponse = $"HTTP GET request to {requestUrl} completed with status code: {result.StatusCode}";
                await kernel.LogInformation($"[make_http_get_request][{DateTime.UtcNow}] {messageResponse}", _logger, _teamsClient);
                return messageResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError($"An error occurred while making HTTP GET Request: {ex.Message}");
                throw new Exception($"An error occurred while making HTTP GET Request: {ex.Message}");
            }
        }
    }
}

