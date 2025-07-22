// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Microsoft.Extensions.Logging;
using FirstPartyAgent.Core.Models;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using FirstPartyAgent.Core.Configuration;

namespace FirstPartyAgent.Core.Services
{
    public sealed class AzureAlertingClient
    {
        private readonly HttpClient? _httpClient;
        private readonly AzureAlertingSettings _azureAlertingSettings;
        private readonly bool isEnabled;
        private readonly ILogger<AzureAlertingClient> _logger;

        public AzureAlertingClient(ILogger<AzureAlertingClient> logger, AzureAlertingSettings azureAlertingSettings)
        {
            _azureAlertingSettings = azureAlertingSettings;
            _logger = logger;
            if (string.IsNullOrWhiteSpace(_azureAlertingSettings.UserToken) && string.IsNullOrWhiteSpace(_azureAlertingSettings.CertificateSubjectName))
            {
                isEnabled = false;
                return;
            }
            isEnabled = true;
            _httpClient = GetHttpClient();
        }

        public bool IsEnabled()
        {
            return isEnabled;
        }

        private HttpClient GetHttpClient()
        {
            HttpClient result;
            var handler = new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = delegate { return true; },
            };
            if (!string.IsNullOrWhiteSpace(_azureAlertingSettings.CertificateSubjectName))
            {
                var cert = CertLoader.LoadCertFromAppService(_azureAlertingSettings.CertificateSubjectName, string.Empty);
                // Removed SslProtocols setting to use OS default
                handler.ClientCertificates.Add(cert);
            }
            result = !string.IsNullOrWhiteSpace(_azureAlertingSettings.CertificateSubjectName) ? new HttpClient(handler) : new HttpClient();
            result.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            result.DefaultRequestHeaders.Add("User-Agent", "sreagent1p");
            result.Timeout = TimeSpan.FromSeconds(30);
            return result;
        }

        private async Task<HttpResponseMessage> SendRequestWithRetryAsync(HttpRequestMessage request, int maxRetries = 3, int initialDelayInMilliseconds = 500)
        {
            if (!isEnabled || _httpClient == null)
            {
                throw new InvalidOperationException("Azure Alerting Client is not enabled. Please check the configuration.");
            }

            HttpResponseMessage? response = null;
            int retries = 0;
            int delay = initialDelayInMilliseconds;

            if (string.IsNullOrWhiteSpace(_azureAlertingSettings.CertificateSubjectName) && !string.IsNullOrWhiteSpace(_azureAlertingSettings.UserToken))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _azureAlertingSettings.UserToken);
            }

            while (retries < maxRetries)
            {
                using (var newRequest = new HttpRequestMessage(request.Method, request.RequestUri))
                {
                    // Copy the headers from the original request  
                    foreach (var header in request.Headers)
                    {
                        newRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
                    }

                    // Copy the request content for POST and PUT requests  
                    if (request.Content != null && (request.Method == HttpMethod.Post || request.Method == HttpMethod.Put))
                    {
                        newRequest.Content = new ByteArrayContent(await request.Content.ReadAsByteArrayAsync());
                        foreach (var header in request.Content.Headers)
                        {
                            newRequest.Content.Headers.TryAddWithoutValidation(header.Key, header.Value);
                        }
                    }

                    try
                    {
                        response = await _httpClient.SendAsync(newRequest);
                        if (response.IsSuccessStatusCode)
                        {
                            break;
                        }
                        else
                        {
                            _logger.LogInformation($"Request failed with status code: {response.StatusCode}, Reason: {response.ReasonPhrase}. Retrying. Numretries: {retries}, MaxRetries: {maxRetries}");

                            // Only retry if we haven't reached the max retries
                            if (retries < maxRetries - 1)
                            {
                                await Task.Delay(delay);
                                delay *= 2;
                            }
                            retries++;
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        if (ex.InnerException is TimeoutException)
                        {
                            _logger.LogInformation($"Request timed out. Retrying. Numretries: {retries}, MaxRetries: {maxRetries}");

                            // Only retry if we haven't reached the max retries
                            if (retries < maxRetries - 1)
                            {
                                await Task.Delay(delay);
                                delay *= 2;
                            }
                            retries++;
                        }
                        else
                        {
                            // If the exception is not a TimeoutException, rethrow the exception  
                            throw;
                        }
                    }
                }
            }

            // If we've exhausted all retries and still don't have a successful response
            if (response == null)
            {
                throw new HttpRequestException($"Request failed after {maxRetries} attempts with no response received.");
            }

            return response;
        }

        /// <summary>
        /// Get alert details for alertId
        /// </summary>
        /// <param name="alertId">Alert Id</param>
        public async Task<AlertDetails> GetAlertDetails(string alertId)
        {
            var requestUri = new Uri(_azureAlertingSettings.Endpoint + "api/alert/" + alertId);
            var request = new HttpRequestMessage()
            {
                RequestUri = requestUri,
                Method = HttpMethod.Get
            };

            var response = await SendRequestWithRetryAsync(request, maxRetries: 1);
            if (response.IsSuccessStatusCode)
            {
                var jsonResponse = await response.Content.ReadAsStringAsync();
                var res = JsonConvert.DeserializeObject<AlertDetails>(jsonResponse) ?? throw new Exception("Failed to deserialize alert details response.");
                return res;
            }
            throw new Exception($"Failed to get alert details. Status code: {response.StatusCode}, Reason: {response.ReasonPhrase}");
        }
    }
}

