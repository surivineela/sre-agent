// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Agent.Core.Helpers;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FirstPartyAgent.Core.Services
{
    public class ICMWorkflowClient
    {
        private readonly bool IsDevelopment;
        private static HttpClient _httpClient;
        private readonly ILogger<ICMWorkflowClient> _logger;
        private readonly ICMWorkflowSettings _icmWorkflowSettings;
        private const string ActionPath = "triggers/manual/execute";
        private readonly int TimeoutInSeconds = 600;
        public bool ProcessImages = true;

        public ICMWorkflowClient(IHostEnvironment environment, ILogger<ICMWorkflowClient> logger, ICMWorkflowSettings icmWorkflowSettings)
        {
            _icmWorkflowSettings = icmWorkflowSettings;
            ProcessImages = _icmWorkflowSettings.ProcessImages;
            IsDevelopment = environment.IsDevelopment();
            _logger = logger;

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
                    throw new Exception("You need to set at least one of the two environment variables - 'ICMWorkflows:CertificateSubjectName' or 'ICMWorkflows:CertificateFilePath'.");
                }
                if (IsDevelopment && string.IsNullOrWhiteSpace(_icmWorkflowSettings.UserToken) && string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateSubjectName) && string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateFilePath))
                {
                    throw new Exception("You need to set at least one of the two environment variables - 'ICMWorkflows:CertificateSubjectName' or 'ICMWorkflows:UserToken'.");
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
                if (IsDevelopment && !string.IsNullOrWhiteSpace(_icmWorkflowSettings.UserToken))
                {
                    _httpClient = new HttpClient()
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };
                    _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {_icmWorkflowSettings.UserToken}");
                }
                else if (!string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateFilePath))
                {
                    var handler = new HttpClientHandler();
                    var certificate = CertLoader.LoadCertFromFile(_icmWorkflowSettings.CertificateFilePath);
                    handler.ClientCertificates.Add(certificate);
                    _logger.LogInformation("Successfully loaded Cert file for ICMWorkflowClient.");
                    _httpClient = new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };
                }
                else
                {
                    var handler = new HttpClientHandler();

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

                    _httpClient = new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };
                }
            }
        }

        private async Task<HttpResponseMessage> SendICMWorkflowRequest(string workflowName, string body, string tenantId = null)
        {
            _logger.LogInformation($"Sending ICM Workflow Request. WorkflowName: {workflowName}, Body: {body}");
            if (string.IsNullOrWhiteSpace(workflowName))
                throw new ArgumentException("Workflow name must be provided.", nameof(workflowName));
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Body must be provided.", nameof(body));
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                tenantId = _icmWorkflowSettings.TenantId;
            }

            if (_icmWorkflowSettings.UseFunctionApp)
            {
                // Construct the complete URL: FunctionAppEndpoint + "/" + api/ExecuteGenevaWorkflow
                var requestUri = $"{_icmWorkflowSettings.FunctionAppEndpoint}/api/ExecuteGenevaWorkflow";
                Dictionary<string, string> requestBody = new Dictionary<string, string>();
                if (!string.IsNullOrWhiteSpace(tenantId))
                {
                    requestBody.Add("tenantId", tenantId);
                }
                requestBody.Add("workflowName", workflowName);
                requestBody.Add("body", body);
                // Send the HTTP POST request.
                using (var content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json"))
                {
                    var response = await _httpClient.PostAsync(requestUri, content);
                    return response;
                }
            }
            else
            {
                string workflowTriggerPath = workflowName.Contains("/triggers/") ? workflowName : $"{workflowName}/{ActionPath}";
                // Construct the complete URL: WorkflowEndpoint + "/" + workflowName + "/" + ActionPath
                var requestUri = $"{_icmWorkflowSettings.WorkflowsEndpoint}/{tenantId}/workflows/{workflowTriggerPath}";

                // Wrap the JSON body in a StringContent object.
                using (var content = new StringContent(body, Encoding.UTF8, "application/json"))
                {
                    // Send the HTTP POST request.
                    _logger.LogInformation("Sending request to ICM Workflow API: {requestUri}", requestUri);
                    var response = await _httpClient.PostAsync(requestUri, content);
                    return response;
                }
            }
        }

        private async Task<HttpResponseMessage> ExecuteGetCallsInICMWorkflowsFunctionApp(string apiPath)
        {
            if (string.IsNullOrWhiteSpace(_icmWorkflowSettings.FunctionAppEndpoint))
            {
                throw new Exception("'ICMWorkflows:FunctionAppEndpoint' is not set in the configuration");
            }

            var response = await _httpClient.GetAsync($"{_icmWorkflowSettings.FunctionAppEndpoint}{apiPath}");
            return response;
        }

        public async Task<Incident> GetIncidentAsync(string incidentId)
        {
            var payload = JsonConvert.SerializeObject(new { incidentId });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.GetIncidentWorkflowName, payload);
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

        public async Task<string> AddAttachmentToIncident(string incidentId, string fileName, string base64Content)
        {
            var payload = JsonConvert.SerializeObject(new { incidentId, fileName, base64Content });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.AddIncidentAttachmentWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return "Success";
            }
            else
            {
                throw new Exception($"Failed to add attachment to the incident: {incidentId}");
            }
        }

        public async Task<SubscriptionDetail> GetSubscriptionDetail(string subscriptionId)
        {
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.SubscriptionDetailWorkflowName, JsonConvert.SerializeObject(new { SubscriptionId = subscriptionId }));
            if (response.IsSuccessStatusCode) {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<SubscriptionDetail>(content);
                _logger.LogInformation($"GetSubscriptionDetail Completed. The subscription OfferType is {result?.OfferType}, QuotaId is {result?.QuotaId}");
                return result;
            }
            _logger.LogError($"Failed to fetch SubscriptionDetail for subscription {subscriptionId}, statusCode: {response.StatusCode}");
            return null;
        }


        public async Task<AcaSubscriptionUsage> GetSubscriptionUsage(string subscriptionId)
        {
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.SubscriptionUsageWorkflowName, JsonConvert.SerializeObject(new { SubscriptionId = subscriptionId }));
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return JsonConvert.DeserializeObject<AcaSubscriptionUsage>(content);
            }
            _logger.LogError($"Failed to fetch SubscriptionUsage for subscription {subscriptionId}, statusCode: {response.StatusCode}");
            return null;
        }

        public async Task<string> SetSubscriptionQuota(string subscriptionId, string region, string quotaType, string quotaLimit)
        {
            const string workflowName = "Workflow-GenevaAction-SetSubscriptionQuota";

            Dictionary<string, string> body = new()
            {
                { "SubscriptionId", subscriptionId },
                { "Region", region },
                { "QuotaType", quotaType },
                { "QuotaLimit", quotaLimit },
            };

            var response = await SendICMWorkflowRequest(workflowName, JsonConvert.SerializeObject(body));
            if (response.IsSuccessStatusCode) { return await response.Content.ReadAsStringAsync(); }
            return null;
        }

        public async Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId, DateTimeOffset? queryFrom = null)
        {
            if (_icmWorkflowSettings.UseFunctionApp)
            {
                var response = await ExecuteGetCallsInICMWorkflowsFunctionApp($"/api/GetIncidentDiscussionEntries?incidentId={incidentId}");
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
                var payload = queryFrom.HasValue
                    ? JsonConvert.SerializeObject(new
                    {
                        incidentId,
                        QueryFrom = queryFrom.Value.ToString("s", System.Globalization.CultureInfo.InvariantCulture)
                    })
                    : JsonConvert.SerializeObject(new { incidentId });
                                
                var response = await SendICMWorkflowRequest(_icmWorkflowSettings.GetIncidentDiscussionEntriesWorkflowName, payload);
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
            if (_icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry, tenantName, owningTeam });
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
            var payload = JsonConvert.SerializeObject(new { incidentId, tag });
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
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry });
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
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry });
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
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry });
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
            var payload = JsonConvert.SerializeObject(new { incidentId, discussionEntry });
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
            var payload = JsonConvert.SerializeObject(new { subscriptionId });
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

        public async Task<string> GetSubDetailsFromGenevaAsync(string subscriptionId)
        {
            var payload = JsonConvert.SerializeObject(new { subscriptionId });
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

        public async Task<string> RebootWorker(string location, string stampName, string role, string roleInstance)
        {
            _logger.LogInformation($"ICMWorkflowClient: Reboot Worker Action called for location: {location} stampName: {stampName}, role: {role}, roleInstance: {roleInstance}");
            if (_icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            string isvmss = "No";
            var payload = JsonConvert.SerializeObject(new { location, stampName, role, roleInstance, isvmss });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.RebootWorkerWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                return $"Failed to reboot worker for location: {location}, stampName: {stampName}, role: {role}, roleInstance: {roleInstance}";
            }
        }

        public async Task<string> GetRedisDeploymentDetailsFromGenevaAsync(string cacheName)
        {
            var payload = JsonConvert.SerializeObject(new { cacheName });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.RedisDeploymentDetailsWorkflowName, payload, _icmWorkflowSettings.RedisTenantId);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                return $"Failed to fetch redis deployment details for cacheName: {cacheName}. StatusCode: {response.StatusCode}. Content: {await response.Content.ReadAsStringAsync()}";
            }
        }

        public async Task<string> GetRedisDeploymentHistoryFromGenevaAsync(string cacheName)
        {
            var payload = JsonConvert.SerializeObject(new { cacheName });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.RedisDeploymentHistoryWorkflowName, payload, _icmWorkflowSettings.RedisTenantId);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                return $"Failed to fetch redis deployment history for cacheName: {cacheName}. StatusCode: {response.StatusCode}. Content: {await response.Content.ReadAsStringAsync()}";
            }
        }

        public async Task<string> RestartWebApp(string subscriptionId, string webappName, string webspaceName)
        {
            if (_icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { subscriptionId, webappName, webspaceName });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.RestartWebAppWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                return $"Failed to restart web app for subscriptionId: {subscriptionId}, webappName: {webappName}, webspaceName: {webspaceName}";
            }
        }

        public async Task<List<CustomField>> GetCustomFieldsAsync(string incidentId)
        {
            var payload = JsonConvert.SerializeObject(new { incidentId });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.GetIncidentWorkflowName, payload);

            if (!response.IsSuccessStatusCode)
            {
                throw new Exception($"Failed to fetch custom fields for incidentId: {incidentId}");
            }

            var content = await response.Content.ReadAsStringAsync();
            var obj = JsonConvert.DeserializeObject<JObject>(content);

            if (obj == null || !obj.TryGetValue("CustomFields", out var customFieldsToken))
            {
                return new List<CustomField>();
            }

            var customFields = customFieldsToken.ToObject<List<CustomField>>();
            return customFields ?? new List<CustomField>();
        }

        public async Task<string> GetAppLensDiagnosticsAsync(
           [Description("Incident ID")] string incidentId)
        {
            var payload = JsonConvert.SerializeObject(new { incidentId });
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

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
}

