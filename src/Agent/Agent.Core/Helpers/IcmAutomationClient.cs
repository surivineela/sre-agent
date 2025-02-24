using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Agent.Core.Helpers
{
    public class IcmAutomationClient
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private readonly string _serviceId;
        private readonly string _icmEndpoint;
        private const int timeoutInSeconds = 30;

        public IcmAutomationClient(IConfiguration configuration)
        {
            _config = configuration;
            _serviceId = _config.GetValue("ICM:ServiceId", string.Empty) ?? throw new Exception("ICM:ServiceId is not set.");
            _icmEndpoint = _config.GetValue("ICM:Endpoint", string.Empty) ?? throw new Exception("ICM:Endpoint is not set.");

            var httpClientHandler = new HttpClientHandler();
            if (!IsDevelopment())
            {
                X509Certificate2 certificate = GetGenevaCertificate();
                if (certificate != null)
                {
                    Console.WriteLine($"Certificate was successfully returned {certificate.Thumbprint} SubjectName - {certificate.Subject} Issuer - {certificate.Issuer}");
                    httpClientHandler.ClientCertificates.Add(certificate);
                }
            }

            _httpClient = new HttpClient(httpClientHandler);
            _httpClient.Timeout = TimeSpan.FromSeconds(timeoutInSeconds);
        }

        private static bool IsDevelopment()
        {
            return Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development";
        }

        private X509Certificate2 GetGenevaCertificate()
        {
            string SubjectName = _config.GetValue("ICM:CertificateSubjectName", string.Empty);
            if (!string.IsNullOrEmpty(SubjectName))
            {
                return CertLoader.LoadCertFromAppService(SubjectName);
            }

            string certFilePath = _config.GetValue("ICM:CertificateFilePath", string.Empty);
            if (!string.IsNullOrEmpty(certFilePath))
            {
                return CertLoader.LoadCertFromFile(certFilePath);
            }

            return null;
        }

        private async Task<HttpResponseMessage> SendRequestWithRetry(HttpRequestMessage requestMessage, bool retry = true)
        {
            // For local development, we recommend developer to use their ICM automation auth token
            // Use this script to acquire the token: https://eng.ms/docs/products/icm/automation/programmaticaccess/authentication#obtain-and-use-an-aad-access-token-in-powershell
            if (IsDevelopment())
            {
                requestMessage.Headers.Add("Authorization", $"Bearer {_config.GetValue("ICM:UserToken", string.Empty)}");
            }


            var cts = new CancellationTokenSource();
            try
            {
                return await _httpClient.SendAsync(requestMessage, cts.Token);
            }
            catch (TaskCanceledException ex)
            {
                if (ex.CancellationToken == cts.Token && retry)
                {
                    return await SendRequestWithRetry(requestMessage, false);
                }
                else
                {
                    throw;
                }
            }
            catch (Exception)
            {
                if (retry)
                {
                    return await SendRequestWithRetry(requestMessage, false);
                }
                else
                {
                    throw;
                }
            }
        }

        public async Task<(bool, T?)> TriggerIcmWorkflowWithResponse<T>(string workflowName, object? body = null, string triggerName = "manual")
        {
            string workflowUrl = $"{_icmEndpoint}/icm/services/{_serviceId}/workflows/{workflowName}/triggers/{triggerName}/execute";
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, workflowUrl);
            if (body != null)
            {
                requestMessage.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            }
            var response = await SendRequestWithRetry(requestMessage, true);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var responseObject = JsonConvert.DeserializeObject<T>(content);
                return (true, responseObject);
            }
            else
            {
                var content = await response.Content.ReadAsStringAsync();
                Console.WriteLine($"Failed to trigger ICM workflow {workflowName} with status code {response.StatusCode} and content {content}");
                return (false, default);
            }
        }

        public async Task<string> TriggerIcmAsyncWorkflow<T>(string workflowName, object? body = null, string triggerName = "manual")
        {
            string workflowUrl = $"{_icmEndpoint}/icm/services/{_serviceId}/workflows/{workflowName}/triggers/{triggerName}/execute";
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, workflowUrl);
            if (body != null)
            {
                requestMessage.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            }
            var response = await SendRequestWithRetry(requestMessage, true);
            response.EnsureSuccessStatusCode();
            var resultUrl = response.Headers.Location?.ToString();
            if (string.IsNullOrEmpty(resultUrl))
            {
                throw new Exception("Failed to retrieve the result url of the ICM workflow");
            }
            return resultUrl;
        }

        public async Task<(bool, T?)> GetWorkflowResult<T>(string resultUrl)
        {
            var requestMessage = new HttpRequestMessage(HttpMethod.Get, resultUrl);
            var response = await _httpClient.SendAsync(requestMessage);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var incident = JsonConvert.DeserializeObject<T>(content);
                return (true, incident);
            }
            else
            {
                return (false, default);
            }
        }
    }
}
