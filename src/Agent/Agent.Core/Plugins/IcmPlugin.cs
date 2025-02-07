using Agent.Core.Helpers;
using Agent.Core.Models.ICM;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace Agent.Core.Plugins
{
    public class IcmPlugin
    {
        private readonly IConfiguration _config;
        private readonly HttpClient _httpClient;
        private const int timeoutInSeconds = 30;

        public IcmPlugin(IConfiguration configuration)
        {
            _config = configuration;
            
            var httpClientHandler = new HttpClientHandler();
            if (!IsDevelopment())
            {
                X509Certificate2 certificate = GetGenevaCertificate();
                Console.WriteLine($"Certificate was successfully returned {certificate.Thumbprint} IssuerName - {certificate.IssuerName} SubjectName - {certificate.SubjectName} Issuer - {certificate.Issuer}");
                httpClientHandler.ClientCertificates.Add(certificate);
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
            return CertLoader.LoadCertFromAppService(SubjectName);
        }

        private async Task<HttpResponseMessage> SendRequestWithRetry(HttpRequestMessage requestMessage, bool retry = true)
        {
            //For local development, we recommend developer to use their ICM automation auth token
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
                    throw ex;
                }
            }
            catch (Exception ex)
            {
                if (retry)
                {
                    return await SendRequestWithRetry(requestMessage, false);
                }
                else
                {
                    throw ex;
                }
            }
        }

        private async Task<HttpResponseMessage> FetchICMIncidentInfo(string incidentId)
        {
            string FetchIncidentUrl = _config.GetValue("ICM:FetchIncidentUrl", string.Empty);
            if (string.IsNullOrWhiteSpace(FetchIncidentUrl))
            {
                throw new Exception("ICM:FetchIncidentUrl is not set in the configuration");
            }
            Dictionary<string, string> body = new Dictionary<string, string>();
            body.Add("incidentId", incidentId);
            var requestMessage = new HttpRequestMessage(HttpMethod.Post, FetchIncidentUrl);
            requestMessage.Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json");
            return await SendRequestWithRetry(requestMessage, true);
        }

        [KernelFunction("get_icm_incident_info")]
        [Description("Get ICM incident information")]
        public async Task<Incident> GetIncidentInfo(
           [Description("Incident ID")] string incidentId)
        {
            var response = await FetchICMIncidentInfo(incidentId);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var incident = JsonConvert.DeserializeObject<Incident>(content);
                return incident;
            }
            else
            {
                Console.WriteLine($"Failed to fetch incident info for incidentId: {incidentId}");
                return null;
            }
        }

        [KernelFunction("get_icm_incidents_by_team")]
        [Description("Gets a list of ICM incidents by Tenant and Team")]
        public async Task<List<Incident>> GetIncidents(
        [Description("The name of the tenant")] string tenant,
        [Description("Comma-separated list of metrics to include")] string metrics)
        {
            return new List<Incident>();
        }

        [KernelFunction("icm_mitigate_incident")]
        [Description("Mitigate an ICM incident")]
        public async Task<bool> MitigateIncident(
        [Description("Id of the incident")] string incidentId,
        [Description("comment/reason for mitigation action")] string reason)
        {
            return false;
        }
    }
}
