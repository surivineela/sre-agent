using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System.Security.Cryptography.X509Certificates;

namespace FirstPartyAgent.Core.Services
{
    public interface IICMAPIClient
    {
        bool IsEnabled();
        Task<Incident> GetIncidentAsync(string incidentId);
        Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId);
    }

    public class ICMAPIClient : IICMAPIClient
    {
        private readonly bool IsDevelopment;
        private readonly ICMAPISettings _icmApiSettings;
        private static HttpClient _httpClient;
        private readonly int TimeoutInSeconds = 60;
        private readonly string IcmAPIPathPrefix;

        public ICMAPIClient(IConfiguration configuration, IHostEnvironment environment, ICMAPISettings icmApiSettings)
        {
            _icmApiSettings = icmApiSettings;
            IsDevelopment = environment.IsDevelopment();

            if (string.IsNullOrWhiteSpace(icmApiSettings.APIEndpoint))
            {
                throw new Exception("The environment variable 'ICMAPI:APIEndpoint' is not set.");
            }
            if (!IsDevelopment && string.IsNullOrWhiteSpace(icmApiSettings.CertificateSubjectName))
            {
                throw new Exception("The environment variable 'ICMAPI:CertificateSubjectName' is not set.");
            }
            if (IsDevelopment && string.IsNullOrWhiteSpace(icmApiSettings.UserToken))
            {
                throw new Exception("The environment variable 'ICMAPI:UserToken' is not set.");
            }

            IcmAPIPathPrefix = IsDevelopment ? "/api2/user/incidentapi" : "api2/cert/incidentapi";

            InitializeHttpClient();
        }

        public bool IsEnabled()
        {
            return _icmApiSettings.Enabled;
        }

        private void InitializeHttpClient()
        {
            if (IsDevelopment)
            {
                _httpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                };
                _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_icmApiSettings.UserToken}");
            }
            else
            {
                var handler = new HttpClientHandler();

                // Open the "My" certificate store in the current user's context.
                using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                {
                    store.Open(OpenFlags.ReadOnly);

                    // Locate the certificate by matching the subject name.
                    var certificates = store.Certificates.Find(X509FindType.FindBySubjectName, _icmApiSettings.CertificateSubjectName, validOnly: false);
                    if (certificates == null || certificates.Count == 0)
                    {
                        throw new Exception($"Certificate with subject matching '{_icmApiSettings.CertificateSubjectName}' not found.");
                    }

                    // Use the first matching certificate.
                    handler.ClientCertificates.Add(certificates[0]);
                }

                _httpClient = new HttpClient(handler)
                {
                    Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                };
            }
        }

        private async Task<HttpResponseMessage> SendICMGetRequestAsync(string apiPath)
        {
            if (string.IsNullOrWhiteSpace(apiPath))
                throw new ArgumentException("apiPath must be provided.", nameof(apiPath));


            var requestUri = $"{_icmApiSettings.APIEndpoint}{apiPath}";
            var response = await _httpClient.GetAsync(requestUri);
            return response;
        }

        public async Task<Incident> GetIncidentAsync(string incidentId)
        {
            var response = await SendICMGetRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})");
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var obj = JsonConvert.DeserializeObject<JObject>(responseString);
                if (obj["IncidentId"] == null && obj["Id"] != null)
                {
                    obj["IncidentId"] = obj["Id"];
                }
                var incident = JsonConvert.DeserializeObject<Incident>(JsonConvert.SerializeObject(obj));
                return incident;
            }
            else
            {
                throw new Exception($"Failed to retrieve incident. Status code: {response.StatusCode}");
            }
        }

        public async Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId)
        {
            var response = await SendICMGetRequestAsync($"{IcmAPIPathPrefix}/incidents({incidentId})/DescriptionEntries?/$inlinecount=allpages");
            if (response.IsSuccessStatusCode)
            {
                var responseString = await response.Content.ReadAsStringAsync();
                var oDataModel = JsonConvert.DeserializeObject<ODataResponse<DiscussionEntry>>(responseString);
                return oDataModel.Value;
            }
            else
            {
                throw new Exception($"Failed to retrieve incident discussion entries. Status code: {response.StatusCode}");
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
