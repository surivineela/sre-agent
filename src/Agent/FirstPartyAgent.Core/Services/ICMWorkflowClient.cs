// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Agent.Core.Helpers;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FirstPartyAgent.Core.Services
{    
    public class ICMWorkflowClient: IICMWorkflowClient
    {
        private readonly bool IsDevelopment;
        private static HttpClient _httpClient;
        private readonly ILogger<ICMWorkflowClient> _logger;
        private readonly ICMWorkflowSettings _icmWorkflowSettings;
        private const string ActionPath = "triggers/manual/execute";
        private readonly int TimeoutInSeconds = 600;
        private bool _processImages = true;
        public bool ProcessImages => _processImages;

        public ICMWorkflowClient(IHostEnvironment environment, ILogger<ICMWorkflowClient> logger, ICMWorkflowSettings icmWorkflowSettings)
        {
            if (!icmWorkflowSettings.Enabled)
            {
                return;
            }
            _icmWorkflowSettings = icmWorkflowSettings;
            _processImages = _icmWorkflowSettings.ProcessImages;
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
                if (!IsDevelopment && string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateSubjectName) && string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateFilePath) && string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateKeyVaultUri))
                {
                    throw new Exception("You need to set at least one of the three environment variables - 'ICMWorkflows:CertificateSubjectName', 'ICMWorkflows:CertificateFilePath' or 'ICMWorkflows:CertificateKeyVaultUri'.");
                }
                if (IsDevelopment && string.IsNullOrWhiteSpace(_icmWorkflowSettings.UserToken) && string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateSubjectName) && string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateFilePath) && string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateKeyVaultUri))
                {
                    throw new Exception("You need to set at least one of the three environment variables - 'ICMWorkflows:CertificateSubjectName', 'ICMWorkflows:UserToken' or 'ICMWorkflows:CertificateKeyVaultUri'.");
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
                else if (!string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateKeyVaultUri) && !string.IsNullOrEmpty(_icmWorkflowSettings.CertificateKeyVaultSecretName))
                {
                    var handler = new HttpClientHandler();
                    var certificate = CertLoader.LoadCertFromKeyVault(_icmWorkflowSettings.CertificateKeyVaultUri, _icmWorkflowSettings.CertificateKeyVaultSecretName, null, _logger);
                    _logger.LogInformation("Successfully loaded Cert from keyvault for ICMWorkflowClient.");
                    _httpClient = new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };
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
            return "Failed to set subscription quota";
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

        private async Task<List<IncidentAdvancedSearchResultItem>> SearchIncidentsWithParametersAsync(int lookbackPeriodInDays, int resultLimit, List<IncidentAdvancedSearchFilter> filters)
        {
            // Enforce a max limit to avoid getting blocked on ICM Kusto cluster
            if (lookbackPeriodInDays > 30)
            {
                lookbackPeriodInDays = 30;
                _logger.LogInformation("Lookback period capped at 30 days to prevent overloading ICM Kusto cluster");
            }
            if (resultLimit > 10)
            {
                resultLimit = 10;
                _logger.LogInformation("Result limit capped at 10 to prevent overloading ICM Kusto cluster");
            }

            // Build the basic Kusto query structure
            var queryBuilder = new StringBuilder();
            queryBuilder.AppendLine($"let lookbackPeriod = ago({lookbackPeriodInDays}d);");
            queryBuilder.AppendLine($"let resultLimit = {resultLimit};");
            queryBuilder.AppendLine("Incidents");
            
            // Add the lookback period filter
            queryBuilder.AppendLine("| where CreateDate > lookbackPeriod");
            
            // Add each custom filter with AND logic
            if (filters != null && filters.Count > 0)
            {
                foreach (var filter in filters)
                {
                    if (!string.IsNullOrWhiteSpace(filter.ColumnName) && !string.IsNullOrWhiteSpace(filter.Operator))
                    {
                        queryBuilder.AppendLine($"| where {filter.ToKustoFilterExpression()}");
                    }
                }
            }

            // Add the standard projection and result limit
            queryBuilder.AppendLine("| summarize arg_max(ModifiedDate, *) by IncidentId");
            queryBuilder.AppendLine("| extend ResponsibleServiceName = OwningTenantName");
            queryBuilder.AppendLine("| extend Id=IncidentId, Title, ResponsibleServiceName, Severity, CreatedDate = CreateDate, State = Status, MitigateDate, ResolveDate, HowFixed");
            queryBuilder.AppendLine("| take resultLimit");

            var query = queryBuilder.ToString();
            _logger.LogInformation($"Executing Kusto query: {query}");
            
            var payload = JsonConvert.SerializeObject(new { query });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.IncidentLookupWorkflowName, payload);
            
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var searchResults = JsonConvert.DeserializeObject<List<IncidentAdvancedSearchResultItem>>(content);
                return searchResults;
            }
            else
            {
                string errorMessage = $"Failed to search incidents with custom filters. Status code: {response.StatusCode}";
                _logger.LogError(errorMessage);
                return new List<IncidentAdvancedSearchResultItem> { new IncidentAdvancedSearchResultItem { Id = errorMessage } };
            }
        }

        /// <summary>
        /// Convenience overload that creates IncidentFilter objects from the provided parameters
        /// </summary>
        /// <param name="columnNames">Names of the columns to filter on</param>
        /// <param name="operators">Operators to use for filtering (e.g., "==", "contains", ">", etc.)</param>
        /// <param name="values">Values to filter for</param>
        /// <param name="lookbackPeriodInDays">Number of days to look back</param>
        /// <param name="resultLimit">Maximum number of results to return</param>
        /// <returns>List of search results matching the criteria</returns>
        public async Task<List<IncidentAdvancedSearchResultItem>> SearchIncidentsWithParametersAsync(
            int lookbackPeriodInDays,
            int resultLimit,
            List<string> columnNames,
            List<string> operators,
            List<string> values)
        {
            if (columnNames == null || operators == null || values == null)
            {
                throw new ArgumentNullException("columnNames, operators, and values cannot be null");
            }

            if (columnNames.Count != operators.Count || columnNames.Count != values.Count)
            {
                throw new ArgumentException("columnNames, operators, and values must have the same number of items");
            }

            List<IncidentAdvancedSearchResultItem> result = new List<IncidentAdvancedSearchResultItem>();
            try
            {
                var filters = new List<IncidentAdvancedSearchFilter>();
                for (int i = 0; i < columnNames.Count; i++)
                {
                    if (!string.IsNullOrWhiteSpace(columnNames[i]) && !string.IsNullOrWhiteSpace(operators[i]))
                    {
                        filters.Add(new IncidentAdvancedSearchFilter(columnNames[i], operators[i], values[i]));
                    }
                }
                result = await SearchIncidentsWithParametersAsync(lookbackPeriodInDays, resultLimit, filters);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error while searching incidents with parameters: {ex.Message}");
                result.Add(new IncidentAdvancedSearchResultItem { Id = $"Error: {ex.Message}" });
            }

            return result;
        }

        public async Task<List<SearchItem>> SearchIncidentsAsync(string searchString, int lookbackPeriodInDays, int resultLimit)
        {
            // Enforce a max limit to avoid getting blocked on ICM Kusto cluster
            if (lookbackPeriodInDays > 90)
            {
                lookbackPeriodInDays = 90;
            }
            if (resultLimit > 100)
            {
                resultLimit = 100;
            }

            var query = $@"let searchString = '{searchString}';
let lookbackPeriod = ago({lookbackPeriodInDays}d);
let resultLimit = {resultLimit};
Incidents
| where CreateDate > lookbackPeriod
| where Title contains searchString
| summarize arg_max(ModifiedDate, *) by IncidentId
| project Id=IncidentId, Severity, Title, State=Status, ResponsibleServiceName=OwningTenantName, CreatedDate=CreateDate, MitigatedDate=MitigateDate, ResolvedDate=ResolveDate, HowFixed
| take resultLimit";
            var payload = JsonConvert.SerializeObject(new { query });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.IncidentLookupWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var searchResults = JsonConvert.DeserializeObject<List<SearchItem>>(content);
                return searchResults;
            }
            else
            {
                string errorMessage = $"Failed to search incidents with searchString: {searchString}";
                return new List<SearchItem> { new SearchItem { Id = errorMessage } };
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
    public class NullableICMWorkflowClient : IICMWorkflowClient
    {
        public void Dispose() { }

        public Task<Incident> GetIncidentAsync(string incidentId) => Task.FromResult<Incident>(null);

        public Task<string> AddAttachmentToIncident(string incidentId, string fileName, string base64Content) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<SubscriptionDetail> GetSubscriptionDetail(string subscriptionId) => Task.FromResult<SubscriptionDetail>(null);

        public Task<AcaSubscriptionUsage> GetSubscriptionUsage(string subscriptionId) => Task.FromResult<AcaSubscriptionUsage>(null);

        public Task<string> SetSubscriptionQuota(string subscriptionId, string region, string quotaType, string quotaLimit) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId, DateTimeOffset? queryFrom = null) =>
            Task.FromResult(new List<DiscussionEntry>());

        public Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantName, string owningTeam) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> AddTagToIncident(string incidentId, string tag) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<List<IncidentAdvancedSearchResultItem>> SearchIncidentsWithParametersAsync(
            int lookbackPeriodInDays,
            int resultLimit,
            List<string> columnNames,
            List<string> operators,
            List<string> values) =>
            Task.FromResult(new List<IncidentAdvancedSearchResultItem>());

        public Task<List<SearchItem>> SearchIncidentsAsync(string searchString, int lookbackPeriodInDays, int resultLimit) =>
            Task.FromResult(new List<SearchItem>());

        public Task<string> DowngradeSeverityAsync(string incidentId, string discussionEntry) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> TransferIncidentToHumanInterventionAsync(string incidentId, string discussionEntry, string humanInterventionServiceName = null, string humanInterventionTeamName = null) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> MarkSubFirstPartyAsync(string subscriptionId) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> GetSubDetailsFromGenevaAsync(string subscriptionId) => Task.FromResult<string>(null);

        public Task<string> RebootWorker(string location, string stampName, string role, string roleInstance) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> GetRedisDeploymentDetailsFromGenevaAsync(string cacheName) => Task.FromResult<string>(null);

        public Task<string> GetRedisDeploymentHistoryFromGenevaAsync(string cacheName) => Task.FromResult<string>(null);

        public Task<string> RestartWebApp(string subscriptionId, string webappName, string webspaceName) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<List<CustomField>> GetCustomFieldsAsync(string incidentId) =>
            Task.FromResult(new List<CustomField>());

        public Task<string> GetAppLensDiagnosticsAsync(string incidentId) => Task.FromResult<string>(null);
        public bool ProcessImages => false;
    }
    public interface IICMWorkflowClient : IDisposable
    {
        Task<Incident> GetIncidentAsync(string incidentId);
        Task<string> AddAttachmentToIncident(string incidentId, string fileName, string base64Content);
        Task<SubscriptionDetail> GetSubscriptionDetail(string subscriptionId);
        Task<AcaSubscriptionUsage> GetSubscriptionUsage(string subscriptionId);
        Task<string> SetSubscriptionQuota(string subscriptionId, string region, string quotaType, string quotaLimit);
        Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId, DateTimeOffset? queryFrom = null);
        Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantName, string owningTeam);
        Task<string> AddTagToIncident(string incidentId, string tag);
        Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry);
        Task<List<IncidentAdvancedSearchResultItem>> SearchIncidentsWithParametersAsync(
            int lookbackPeriodInDays,
            int resultLimit,
            List<string> columnNames,
            List<string> operators,
            List<string> values);
        Task<List<SearchItem>> SearchIncidentsAsync(string searchString, int lookbackPeriodInDays, int resultLimit);
        Task<string> DowngradeSeverityAsync(string incidentId, string discussionEntry);
        Task<string> TransferIncidentToHumanInterventionAsync(string incidentId, string discussionEntry, string humanInterventionServiceName = null, string humanInterventionTeamName = null);
        Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry);
        Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry);
        Task<string> MarkSubFirstPartyAsync(string subscriptionId);
        Task<string> GetSubDetailsFromGenevaAsync(string subscriptionId);
        Task<string> RebootWorker(string location, string stampName, string role, string roleInstance);
        Task<string> GetRedisDeploymentDetailsFromGenevaAsync(string cacheName);
        Task<string> GetRedisDeploymentHistoryFromGenevaAsync(string cacheName);
        Task<string> RestartWebApp(string subscriptionId, string webappName, string webspaceName);
        Task<List<CustomField>> GetCustomFieldsAsync(string incidentId);
        Task<string> GetAppLensDiagnosticsAsync(string incidentId);
        bool ProcessImages { get; }
    }
}

