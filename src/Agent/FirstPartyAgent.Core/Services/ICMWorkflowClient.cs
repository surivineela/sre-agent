using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.Json;
using Agent.Core.Helpers;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace FirstPartyAgent.Core.Services
{
    public class ICMWorkflowClient : IDisposable
    {
        private static HttpClient _httpClient;

        private readonly bool IsDevelopment;
        private readonly ILogger<ICMWorkflowClient> _logger;
        private readonly ICMWorkflowSettings _icmWorkflowSettings;
        private const string ActionPath = "triggers/manual/execute";
        private readonly int TimeoutInSeconds = 600;

        public ICMWorkflowClient(ILogger<ICMWorkflowClient> logger, ICMWorkflowSettings icmWorkflowSettings, IHostEnvironment environment)
        {
            _icmWorkflowSettings = icmWorkflowSettings;
            _logger = logger;
            IsDevelopment = environment.IsDevelopment();

            if (_icmWorkflowSettings.UseFunctionApp)
            {
                if (string.IsNullOrWhiteSpace(_icmWorkflowSettings.FunctionAppEndpoint))
                {
                    throw new Exception("The environment variable 'ICMWorkflows:FunctionAppEndpoint' is not set.");
                }
                if (string.IsNullOrWhiteSpace(_icmWorkflowSettings.FunctionAppKey))
                {
                    throw new Exception("The environment variable 'ICMWorkflows:FunctionAppKey' is not set.");
                }
            }
            else
            {
                if (string.IsNullOrWhiteSpace(_icmWorkflowSettings.WorkflowsEndpoint))
                {
                    throw new Exception("The environment variable 'ICMWorkflows:WorkflowsEndpoint' is not set.");
                }
                if (!IsDevelopment && string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateSubjectName) && string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateFilePath))
                {
                    throw new Exception("The environment variable 'ICMWorkflows:CertificateSubjectName' or 'ICMWorkflows:CertificateFilePath' is not set.");
                }
                if (IsDevelopment && string.IsNullOrWhiteSpace(_icmWorkflowSettings.UserToken))
                {
                    throw new Exception("The environment variable 'ICMWorkflows:UserToken' is not set.");
                }
            }

            InitializeHttpClient();
        }

        private void InitializeHttpClient()
        {
            if (_icmWorkflowSettings.UseFunctionApp)
            {
                _httpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                };
                _httpClient.DefaultRequestHeaders.Add("x-functions-key", _icmWorkflowSettings.FunctionAppKey);
            }
            else
            {
                if (IsDevelopment)
                {
                    // Use this script to acquire the token: https://eng.ms/docs/products/icm/automation/programmaticaccess/authentication#obtain-and-use-an-aad-access-token-in-powershell
                    _httpClient = new HttpClient()
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_icmWorkflowSettings.UserToken}");
                }
                else
                {
                    var handler = new HttpClientHandler();

                    if (!string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateSubjectName))
                    {
                        // Open the "My" certificate store in the current user's context.
                        using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser))
                        {
                            store.Open(OpenFlags.ReadOnly);

                            // Locate the certificate by matching the subject name.
                            var certificates = store.Certificates.Find(X509FindType.FindBySubjectName, _icmWorkflowSettings.CertificateSubjectName, validOnly: false);
                            if (certificates == null || certificates.Count == 0)
                            {
                                throw new Exception($"Certificate with subject matching '{_icmWorkflowSettings.CertificateSubjectName}' not found.");
                            }

                            // Use the first matching certificate.
                            handler.ClientCertificates.Add(certificates[0]);
                        }
                    }
                    else
                    {
                        var certificate = CertLoader.LoadCertFromFile(_icmWorkflowSettings.CertificateFilePath);
                        handler.ClientCertificates.Add(certificate);
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

            if (_icmWorkflowSettings.UseFunctionApp)
            {
                // Construct the complete URL: FunctionAppEndpoint + "/" + api/ExecuteGenevaWorkflow
                var requestUri = $"{_icmWorkflowSettings.FunctionAppEndpoint}/api/ExecuteGenevaWorkflow";
                Dictionary<string, string> requestBody = new Dictionary<string, string>();
                requestBody.Add("workflowName", workflowName);
                requestBody.Add("body", body);
                // Send the HTTP POST request.
                using (var content = new StringContent(JsonSerializer.Serialize(requestBody), Encoding.UTF8, "application/json"))
                {
                    var response = await _httpClient.PostAsync(requestUri, content);
                    return response;
                }
            }
            else
            {
                // Construct the complete URL: WorkflowEndpoint + "/" + workflowName + "/" + ActionPath
                var requestUri = $"{_icmWorkflowSettings.WorkflowsEndpoint}/{workflowName}/{ActionPath}";

                // Wrap the JSON body in a StringContent object.
                using (var content = new StringContent(body, Encoding.UTF8, "application/json"))
                {
                    // Send the HTTP POST request.
                    var response = await _httpClient.PostAsync(requestUri, content);
                    return response;
                }
            }
        }

        public async Task<TResponse> SendICMWorkflowRequest<TResponse>(string workflowName, object requestObject)
        {
            var payload = JsonSerializer.Serialize(requestObject);

            var response = await SendICMWorkflowRequest(workflowName, payload);

            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var responseObject = JsonSerializer.Deserialize<TResponse>(content);
                return responseObject;
            }
            else
            {
                throw new IcmWorkflowException(workflowName, response);
            }
        }

        private async Task<HttpResponseMessage> ExecuteGetCallsInICMWorkflowsFunctionApp(string apiPath)
        {
            if (string.IsNullOrWhiteSpace(_icmWorkflowSettings.FunctionAppEndpoint))
            {
                throw new Exception("'ICMWorkflows:FunctionAppEndpoint' is not set in the configuration");
            }

            var response = await _httpClient.GetAsync($"{_icmWorkflowSettings.FunctionAppEndpoint}{apiPath}");
            response.EnsureSuccessStatusCode();
            return response;
        }

        public async Task<Incident> GetIncidentAsync(string incidentId)
        {
            return await SendICMWorkflowRequest<Incident>(_icmWorkflowSettings.GetIncidentWorkflowName, new { incidentId });
        }

        public async Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId, DateTimeOffset? queryFrom = null)
        {
            if (_icmWorkflowSettings.UseFunctionApp)
            {
                var response = await ExecuteGetCallsInICMWorkflowsFunctionApp($"/api/GetDiscussionEntries?incidentId={incidentId}");
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var resObj = JsonSerializer.Deserialize<ODataResponse<DiscussionEntry>>(content);
                    return resObj.Value;
                }
                else
                {
                    _logger.LogError($"Failed to fetch discussion entries for incidentId: {incidentId}");
                    return null;
                }
            }
            else
            {
                // if queryFrom is not provided, then only query for the last 30 days
                if (!queryFrom.HasValue)
                {
                    queryFrom = DateTimeOffset.UtcNow.AddDays(-30);
                }
                var payload = JsonSerializer.Serialize(new { IncidentId = incidentId, QueryFrom = queryFrom?.ToString("s", System.Globalization.CultureInfo.InvariantCulture) });
                var response = await SendICMWorkflowRequest(_icmWorkflowSettings.GetIncidentDiscussionEntriesWorkflowName, payload);
                if (response.IsSuccessStatusCode)
                {
                    var content = await response.Content.ReadAsStringAsync();
                    var discussionEntries = JsonSerializer.Deserialize<List<DiscussionEntry>>(content);
                    return discussionEntries;
                }
                else
                {
                    _logger.LogError($"Failed to fetch discussion entries for incidentId: {incidentId}");
                    return null;
                }
            }
        }

        public async Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantName, string owningTeam)
        {
            if (_icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonSerializer.Serialize(new { incidentId, discussionEntry, tenantName, owningTeam });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.TransferIncidentWorkflowName, payload);
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
            if (_icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonSerializer.Serialize(new { incidentId, tag });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.AddIncidentTagWorkflowName, payload);
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
            if (_icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonSerializer.Serialize(new { incidentId, discussionEntry });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.MitigateIncidentWorkflowName, payload);
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
            if (_icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonSerializer.Serialize(new { incidentId, discussionEntry });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.DowngradeSev2WorkflowName, payload);
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
            if (_icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }

            return await TransferIncidentAsync(incidentId, discussionEntry,
                humanInterventionServiceName ?? _icmWorkflowSettings.HumanInterventionServiceName,
                humanInterventionTeamName ?? _icmWorkflowSettings.HumanInterventionTeamName);
        }

        public async Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry)
        {
            if (_icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonSerializer.Serialize(new { IncidentId = incidentId, Message = discussionEntry });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.ResolveIncidentWorkflowName, payload);
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
            if (_icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonSerializer.Serialize(new { incidentId = incidentId, text = discussionEntry });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.PostIncidentDiscussionWorkflowName, payload);
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
            if (_icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonSerializer.Serialize(new { subscriptionId });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.MarkSubscriptionFirstPartyWorkflowName, payload);
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
        public async Task<SubscriptionDetail> GetSubscriptionDetail(string subscriptionId)
        {
            return await SendICMWorkflowRequest<SubscriptionDetail>(_icmWorkflowSettings.SubscriptionDetailWorkflowName, new { SubscriptionId = subscriptionId });
        }

        public async Task<string> GetSubDetailsFromGenevaAsync(string subscriptionId)
        {
            var payload = JsonSerializer.Serialize(new { subscriptionId });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.GetSubscriptionWorkflowName, payload);
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
            var payload = JsonSerializer.Serialize(new { incidentId });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.ApplensPluginWorkflowName, payload);
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

        public async Task<AcaSubscriptionUsage> GetSubscriptionUsage(string subscriptionId)
        {
            return await SendICMWorkflowRequest<AcaSubscriptionUsage>(_icmWorkflowSettings.SubscriptionUsageWorkflowName, new { SubscriptionId = subscriptionId });
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public class IcmWorkflowException : Exception
    {
        public IcmWorkflowException(string workflowName, HttpResponseMessage responseMessage)
            : base(GetErrorMessage(workflowName, responseMessage))
        {
        }

        private static string GetErrorMessage(string workflowName, HttpResponseMessage responseMessage)
        {
            string content = responseMessage.Content.ReadAsStringAsync().Result;

            return $"Failed to execute workflow '{workflowName}', Error: {content}";
        }
    }
}
