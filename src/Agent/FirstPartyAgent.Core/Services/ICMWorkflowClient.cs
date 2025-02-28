using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Text;

namespace FirstPartyAgent.Core.Services
{
    public class ICMWorkflowClient
    {
        private readonly bool IsDevelopment;
        private static HttpClient _httpClient;
        private readonly ICMWorkflowSettings icmWorkflowSettings;
        private const string ActionPath = "triggers/manual/execute";
        private readonly int TimeoutInSeconds = 600;

        public ICMWorkflowClient(IConfiguration configuration, IHostEnvironment environment)
        {
            icmWorkflowSettings = configuration.GetSection("ICMWorkflows").Get<ICMWorkflowSettings>();
            IsDevelopment = environment.IsDevelopment();

            if (icmWorkflowSettings.UseFunctionApp)
            {
                if (string.IsNullOrWhiteSpace(icmWorkflowSettings.FunctionAppEndpoint))
                {
                    throw new Exception("The environment variable 'ICMWorkflows:FunctionAppEndpoint' is not set.");
                }
                if (string.IsNullOrWhiteSpace(icmWorkflowSettings.FunctionAppKey))
                {
                    throw new Exception("The environment variable 'ICMWorkflows:FunctionAppKey' is not set.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(icmWorkflowSettings.WorkflowsEndpoint))
                {
                    throw new Exception("The environment variable 'ICMWorkflows:WorkflowsEndpoint' is not set.");
                }
                if (!IsDevelopment && string.IsNullOrWhiteSpace(icmWorkflowSettings.CertificateSubjectName))
                {
                    throw new Exception("The environment variable 'ICMWorkflows:CertificateSubjectName' is not set.");
                }
                if (IsDevelopment && string.IsNullOrWhiteSpace(icmWorkflowSettings.UserToken))
                {
                    throw new Exception("The environment variable 'ICMWorkflows:UserToken' is not set.");
                }
            }

            InitializeHttpClient();
        }

        private void InitializeHttpClient()
        {
            if (icmWorkflowSettings.UseFunctionApp)
            {
                _httpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                };
                _httpClient.DefaultRequestHeaders.Add("x-functions-key", icmWorkflowSettings.FunctionAppKey);
            }
            else
            {
                if (IsDevelopment)
                {
                    _httpClient = new HttpClient()
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {icmWorkflowSettings.UserToken}");
                }
                else
                {
                    var handler = new HttpClientHandler();

                    // Open the "My" certificate store in the current user's context.
                    using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                    {
                        store.Open(OpenFlags.ReadOnly);

                        // Locate the certificate by matching the subject name.
                        var certificates = store.Certificates.Find(X509FindType.FindBySubjectName, icmWorkflowSettings.CertificateSubjectName, validOnly: false);
                        if (certificates == null || certificates.Count == 0)
                        {
                            throw new Exception($"Certificate with subject matching '{icmWorkflowSettings.CertificateSubjectName}' not found.");
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
        }

        private async Task<HttpResponseMessage> SendICMWorkflowRequest(string workflowName, string body)
        {
            if (string.IsNullOrWhiteSpace(workflowName))
                throw new ArgumentException("Workflow name must be provided.", nameof(workflowName));
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Body must be provided.", nameof(body));

            if (icmWorkflowSettings.UseFunctionApp)
            {
                // Construct the complete URL: FunctionAppEndpoint + "/" + api/ExecuteGenevaWorkflow
                var requestUri = $"{icmWorkflowSettings.FunctionAppEndpoint}/api/ExecuteGenevaWorkflow";
                Dictionary<string, string> requestBody = new Dictionary<string, string>();
                requestBody.Add("workflowName", workflowName);
                requestBody.Add("body", body);
                // Send the HTTP POST request.
                using (var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json"))
                {
                    var response = await _httpClient.PostAsync(requestUri, content);
                    response.EnsureSuccessStatusCode();
                    return response;
                }
            }
            else
            {
                // Construct the complete URL: WorkflowEndpoint + "/" + workflowName + "/" + ActionPath
                var requestUri = $"{icmWorkflowSettings.WorkflowsEndpoint}/{workflowName}/{ActionPath}";

                // Wrap the JSON body in a StringContent object.
                using (var content = new StringContent(body, Encoding.UTF8, "application/json"))
                {
                    // Send the HTTP POST request.
                    var response = await _httpClient.PostAsync(requestUri, content);
                    response.EnsureSuccessStatusCode();
                    return response;
                }
            }
        }

        private async Task<HttpResponseMessage> ExecuteGetCallsInICMWorkflowsFunctionApp(string apiPath)
        {
            if (string.IsNullOrWhiteSpace(icmWorkflowSettings.FunctionAppEndpoint))
            {
                throw new Exception("'ICMWorkflows:FunctionAppEndpoint' is not set in the configuration");
            }

            var response = await _httpClient.GetAsync($"{icmWorkflowSettings.FunctionAppEndpoint}{apiPath}");
            response.EnsureSuccessStatusCode();
            return response;
        }

        public async Task<Incident> GetIncidentAsync(string incidentId)
        {
            var payload = JsonConvert.SerializeObject(new { incidentId });
            var response = await SendICMWorkflowRequest(icmWorkflowSettings.GetIncidentWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var incident = JsonConvert.DeserializeObject<Incident>(content);
                return incident;
            }
            else
            {
                throw new Exception($"Failed to fetch incident info for incidentId: {incidentId}");
            }
        }

        public async Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId)
        {
            if (icmWorkflowSettings.UseFunctionApp)
            {
                var response = await ExecuteGetCallsInICMWorkflowsFunctionApp($"/api/GetDiscussionEntries?incidentId={incidentId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var resObj = JsonConvert.DeserializeObject<ODataResponse<DiscussionEntry>>(content);
                    return resObj.Value;
                }
                else
                {
                    Console.WriteLine($"Failed to fetch discussion entries for incidentId: {incidentId}");
                    return null;
                }
            }
            else
            {
                var payload = JsonConvert.SerializeObject(new { incidentId });
                var response = await SendICMWorkflowRequest(icmWorkflowSettings.GetIncidentDiscussionEntriesWorkflowName, payload);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var discussionEntries = JsonConvert.DeserializeObject<List<DiscussionEntry>>(content);
                    return discussionEntries;
                }
                else
                {
                    Console.WriteLine($"Failed to fetch discussion entries for incidentId: {incidentId}");
                    return null;
                }
            }
        }

        public async Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantName, string owningTeam)
        {
            if (icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry, tenantName, owningTeam });
            var response = await SendICMWorkflowRequest(icmWorkflowSettings.TransferIncidentWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                return "Success";
            }
            else
            {
                string errorMessage = $"Failed to transfer incident for incidentId: {incidentId}";
                return errorMessage;
            }
        }

        public async Task<string> AddTagToIncident(string incidentId, string tag)
        {
            if (icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, tag });
            var response = await SendICMWorkflowRequest(icmWorkflowSettings.AddIncidentTagWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                return "Success";
            }
            else
            {
                string errorMessage = $"Failed to add tag to incident incident for incidentId: {incidentId}";
                return errorMessage;
            }
        }

        public async Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry)
        {
            if (icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry });
            var response = await SendICMWorkflowRequest(icmWorkflowSettings.MitigateIncidentWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                return "Success";
            }
            else
            {
                string errorMessage = $"Failed to mitigate incident for incidentId: {incidentId}";
                return errorMessage;
            }
        }

        public async Task<string> DowngradeSeverityAsync(string incidentId, string discussionEntry)
        {
            if (icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry });
            var response = await SendICMWorkflowRequest(icmWorkflowSettings.DowngradeSev2WorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                return "Success";
            }
            else
            {
                string errorMessage = $"Failed to downgrade severity of incident for incidentId: {incidentId}";
                return errorMessage;
            }
        }

        public async Task<string> TransferIncidentToHumanInterventionAsync(string incidentId, string discussionEntry, string humanInterventionServiceName = null, string humanInterventionTeamName = null)
        {
            if (icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }

            return await TransferIncidentAsync(incidentId, discussionEntry,
                humanInterventionServiceName?? icmWorkflowSettings.HumanInterventionServiceName,
                humanInterventionTeamName?? icmWorkflowSettings.HumanInterventionTeamName);
        }

        public async Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry)
        {
            if (icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry });
            var response = await SendICMWorkflowRequest(icmWorkflowSettings.ResolveIncidentWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                return "Success";
            }
            else
            {
                string errorMessage = $"Failed to resolve incident for incidentId: {incidentId}";
                return errorMessage;
            }
        }

        public async Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry)
        {
            if (icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry });
            var response = await SendICMWorkflowRequest(icmWorkflowSettings.PostIncidentDiscussionWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                return "Success";
            }
            else
            {
                string errorMessage = $"Failed to post discussion entry for incidentId: {incidentId}";
                return errorMessage;
            }
        }

        public async Task<string> MarkSubFirstPartyAsync(string subscriptionId)
        {
            if (icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { subscriptionId });
            var response = await SendICMWorkflowRequest(icmWorkflowSettings.MarkSubscriptionFirstPartyWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                string errorMessage = $"Failed to mark subscription as first party for subscriptionId: {subscriptionId}";
                return errorMessage;
            }
        }

        public async Task<string> GetSubDetailsFromGenevaAsync(string subscriptionId)
        {
            var payload = JsonConvert.SerializeObject(new { subscriptionId });
            var response = await SendICMWorkflowRequest(icmWorkflowSettings.GetSubscriptionWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                return $"Failed to fetch sub details for subscriptionId: {subscriptionId}";
            }
        }

        public async Task<string> GetAppLensDiagnosticsAsync(
           [Description("Incident ID")] string incidentId)
        {
            var payload = JsonConvert.SerializeObject(new { incidentId });
            var response = await SendICMWorkflowRequest(icmWorkflowSettings.ApplensPluginWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                return $"Failed to fetch AppLens diagnostics for incidentId: {incidentId}";
            }
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}
