// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Helpers;
using Newtonsoft.Json;
using System.Net;
using System.Net.Http.Headers;

namespace FirstPartyAgent.Core.Services
{
    public class ObserverResponse
    {
        public HttpStatusCode StatusCode;

        public dynamic Content;
    }

    public sealed class ObserverClientService
    {
        private readonly ObserverClientSettings _observerClientSettings;
        private static HttpClient _httpClient;

        public bool IsEnabled => _observerClientSettings.IsEnabled;

        public ObserverClientService(ObserverClientSettings observerClientSettings)
        {
            _observerClientSettings = observerClientSettings;
            if (_observerClientSettings.IsEnabled && (string.IsNullOrWhiteSpace(_observerClientSettings.CertificateSubjectName) && !_observerClientSettings.UserAuth))
            {
                throw new Exception("Either CertificateSubjectName or UserAuth must be set to run observer client");
            }
            InitializeHttpClient();
        }

        private void InitializeHttpClient()
        {
            var handler = new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = delegate { return true; },
            };
            if (!string.IsNullOrWhiteSpace(_observerClientSettings.CertificateSubjectName))
            {
                var cert = CertLoader.LoadCertFromAppService(_observerClientSettings.CertificateSubjectName, string.Empty);
                // Removed SslProtocols setting to use OS default
                handler.ClientCertificates.Add(cert);
            }
            _httpClient = !string.IsNullOrWhiteSpace(_observerClientSettings.CertificateSubjectName) ? new HttpClient(handler) : new HttpClient();
            _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            _httpClient.DefaultRequestHeaders.Add("User-Agent", "sreagent1p");
            _httpClient.Timeout = TimeSpan.FromSeconds(30);
        }

        private async Task<HttpResponseMessage> SendRequestWithRetryAsync(HttpRequestMessage request, int maxRetries = 1, int initialDelayInMilliseconds = 500)
        {
            HttpResponseMessage response = null;
            int retries = 0;
            int delay = initialDelayInMilliseconds;
            
            if (string.IsNullOrWhiteSpace(_observerClientSettings.CertificateSubjectName) && _observerClientSettings.UserAuth)
            {
                var observerToken = await LocalAadAuthenticator.AcquireTokenAsync($"{_observerClientSettings.UserAuthClientId}/.default");
                request.Headers.Add("Authorization", observerToken);
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
                            await Task.Delay(delay);
                            retries++;
                            delay *= 2;
                        }
                    }
                    catch (HttpRequestException ex)
                    {
                        if (ex is TimeoutException || ex.InnerException is TimeoutException)
                        {
                            // If the exception is a TimeoutException, wait and retry  
                            await Task.Delay(delay);
                            retries++;
                            delay *= 2;
                        }
                        else
                        {
                            // If the exception is not a TimeoutException, rethrow the exception  
                            throw;
                        }
                    }
                }
            }

            return response;
        }

        /// <summary>
        /// Get site details for siteName
        /// </summary>
        /// <param name="siteName">Site Name</param>
        public async Task<ObserverResponse> GetSite(string siteName)
        {
            return await GetSiteInternal(_observerClientSettings.Endpoint + "sites/" + siteName + "/adminsites");
        }

        /// <summary>
        /// Get site details for siteName
        /// </summary>
        /// <param name="stamp">Stamp</param>
        /// <param name="siteName">Site Name</param>
        /// <param name="details">True if additional properties are requested</param>
        public async Task<ObserverResponse> GetSite(string stamp, string siteName, bool details = false)
        {
            return await GetSiteInternal(_observerClientSettings.Endpoint + "stamps/" + stamp + "/sites/" + siteName + (details ? "/details" : ""));
        }

        private async Task<ObserverResponse> GetSiteInternal(string endpoint)
        {
            return await GetAppInternal(endpoint, "GetAdminSite");
        }

        public async Task<ObserverResponse> GetContainerApp(string containerAppName)
        {
            return await GetContainerAppInternal(_observerClientSettings.Endpoint + "partner/containerapp/" + containerAppName);
        }

        private async Task<ObserverResponse> GetContainerAppInternal(string endpoint)
        {
            return await GetAppInternal(endpoint, "GetContainerApp");
        }

        private async Task<ObserverResponse> GetAppInternal(string endpoint, string apiName)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(endpoint),
                Method = HttpMethod.Get
            };

            var response = await SendRequestWithRetryAsync(request);

            ObserverResponse res = await CreateObserverResponse(response, apiName);
            return res;
        }

        public async Task<ObserverResponse> GetStamp(string stampName)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(_observerClientSettings.Endpoint + "stamps/" + stampName),
                Method = HttpMethod.Get
            };

            var response = await SendRequestWithRetryAsync(request);

            ObserverResponse res = await CreateObserverResponse(response, "GetStamp");
            return res;
        }

        public async Task<ObserverResponse> GetHostingEnvironmentDetails(string hostingEnvironmentName)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri(_observerClientSettings.Endpoint + "hostingEnvironments/" + hostingEnvironmentName),
                Method = HttpMethod.Get
            };

            var response = await SendRequestWithRetryAsync(request);

            ObserverResponse res = await CreateObserverResponse(response, "GetHostingEnvironmentDetails(2.0)");
            return res;
        }

        public async Task<ObserverResponse> GetSitePostBody(string stamp, string site)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri($"{_observerClientSettings.Endpoint}stamps/{stamp}/sites/{site}/postbody"),
                Method = HttpMethod.Get
            };

            var response = await SendRequestWithRetryAsync(request);

            ObserverResponse res = await CreateObserverResponse(response, "GetSitePostBody");
            return res;
        }

        public async Task<ObserverResponse> GetHostingEnvironmentPostBody(string name)
        {
            var request = new HttpRequestMessage()
            {
                RequestUri = new Uri($"{_observerClientSettings.Endpoint}hostingEnvironments/{name}/postbody"),
                Method = HttpMethod.Get
            };

            var response = await SendRequestWithRetryAsync(request);

            ObserverResponse res = await CreateObserverResponse(response, "GetHostingEnvironmentPostBody");
            return res;
        }

        private async Task<ObserverResponse> CreateObserverResponse(HttpResponseMessage response, string apiName = "")
        {
            var observerResponse = new ObserverResponse();

            if (response == null)
            {
                observerResponse.StatusCode = HttpStatusCode.InternalServerError;
                observerResponse.Content = "Unable to fetch data from Observer API : " + apiName;
            }

            observerResponse.StatusCode = response.StatusCode;

            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                observerResponse.Content = JsonConvert.DeserializeObject(responseString);
            }
            else if (response.StatusCode == HttpStatusCode.NotFound)
            {
                observerResponse.Content = "Resource Not Found. API : " + apiName;
            }
            else
            {
                observerResponse.Content = "Unable to fetch data from Observer API : " + apiName;
            }

            return observerResponse;
        }
    }
}

