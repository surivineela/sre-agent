// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using Agent.Core.Helpers;
using Agent.Plugins.Kusto;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Extensions;
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
        private readonly HttpClient? _httpClient;
        private readonly ILogger<ICMWorkflowClient> _logger;
        private readonly ICMWorkflowSettings _icmWorkflowSettings;
        private const string ActionPath = "triggers/manual/execute";
        private readonly int TimeoutInSeconds = 600;
        private bool _processImages = true;
        public bool ProcessImages => _processImages;

        public ICMWorkflowClient(IHostEnvironment environment, ILogger<ICMWorkflowClient> logger, ICMWorkflowSettings icmWorkflowSettings)
        {
            _icmWorkflowSettings = icmWorkflowSettings;
            _processImages = _icmWorkflowSettings.ProcessImages;
            IsDevelopment = environment.IsDevelopment();
            _logger = logger;

            if (!icmWorkflowSettings.Enabled)
            {
                return;
            }

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

            _httpClient = GetHttpClient();
        }

        private HttpClient GetHttpClient()
        {
            HttpClient result;

            if (_icmWorkflowSettings.UseFunctionApp)
            {
                result = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                };
                result.DefaultRequestHeaders.Add("x-functions-key", _icmWorkflowSettings.FunctionAppKey);
            }
            else
            {
                if (IsDevelopment && !string.IsNullOrWhiteSpace(_icmWorkflowSettings.UserToken))
                {
                    result = new HttpClient()
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };
                    result.DefaultRequestHeaders.Add("Authorization", $"Bearer {_icmWorkflowSettings.UserToken}");
                }
                else if ((!string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateSubjectName) || !string.IsNullOrEmpty(_icmWorkflowSettings.CertificateKeyVaultSecretName))
                        && (!string.IsNullOrWhiteSpace(KeyVaultConfigurationExtension.GetPlatformKeyVaultSettingFromEnvironment("KeyVaultUri"))
                            || !string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateKeyVaultUri)))
                {
                    string certKvSecretName = !string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateSubjectName)
                                             ? _icmWorkflowSettings.CertificateSubjectName
                                             : _icmWorkflowSettings.CertificateKeyVaultSecretName;

                    string keyVaultUri = !string.IsNullOrWhiteSpace(KeyVaultConfigurationExtension.GetPlatformKeyVaultSettingFromEnvironment("KeyVaultUri"))
                                            ? KeyVaultConfigurationExtension.GetPlatformKeyVaultSettingFromEnvironment("KeyVaultUri")
                                            : _icmWorkflowSettings.CertificateKeyVaultUri;

                    string certMsi = KeyVaultConfigurationExtension.GetPlatformKeyVaultSettingFromEnvironment("Identity");
                    if (string.IsNullOrWhiteSpace(KeyVaultConfigurationExtension.GetPlatformKeyVaultSettingFromEnvironment("KeyVaultUri")))
                    {
                        certMsi = _icmWorkflowSettings.ManagedIdentityClientId ?? string.Empty;
                    }

                    var certificate = CertLoader.LoadCertFromKeyVault(keyVaultUri, certKvSecretName, certMsi, null, _logger);

                    var handler = new HttpClientHandler();
                    handler.ClientCertificates.Add(certificate);
                    _logger.LogInformation("Successfully loaded Cert from keyvault for ICMWorkflowClient.");
                    result = new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };

                }
                else if (!string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateKeyVaultUri) && !string.IsNullOrEmpty(_icmWorkflowSettings.CertificateKeyVaultSecretName))
                {
                    var handler = new HttpClientHandler();
                    var certificate = CertLoader.LoadCertFromKeyVault(_icmWorkflowSettings.CertificateKeyVaultUri, _icmWorkflowSettings.CertificateKeyVaultSecretName, _icmWorkflowSettings.ManagedIdentityClientId, null, _logger);
                    handler.ClientCertificates.Add(certificate);
                    _logger.LogInformation("Successfully loaded Cert from keyvault for ICMWorkflowClient.");
                    result = new HttpClient(handler)
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
                    result = new HttpClient(handler)
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

                    result = new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };
                }
            }

            return result;
        }

        private async Task<HttpResponseMessage> SendICMWorkflowRequest(string workflowName, string body, string? tenantId = null)
        {
            if (_httpClient == null)
                            {
                throw new InvalidOperationException("ICMWorkflowClient is not properly initialized. HttpClient is null.");
            }

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

        public async Task<Incident> GetIncidentAsync(string incidentId)
        {
            var payload = JsonConvert.SerializeObject(new { incidentId });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.GetIncidentWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var incident = JsonConvert.DeserializeObject<Incident>(content);
                if (incident == null)
                {
                    throw new Exception($"Failed to deserialize incident for incidentId: {incidentId}");
                }
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

        public async Task<string> SetSubscriptionQuota(string subscriptionId, AzureRegion region, string quotaType, string quotaLimit)
        {
            const string workflowName = "Workflow-GenevaAction-SetSubscriptionQuota";

            Dictionary<string, string> body = new()
            {
                { "SubscriptionId", subscriptionId },
                { "Region", region.ToNormalizedString() },
                { "QuotaType", quotaType },
                { "QuotaLimit", quotaLimit },
            };

            var response = await SendICMWorkflowRequest(workflowName, JsonConvert.SerializeObject(body));
            if (response.IsSuccessStatusCode) { return await response.Content.ReadAsStringAsync(); }
            return "Failed to set subscription quota";
        }

        public async Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId, DateTimeOffset? queryFrom = null)
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
                var discussionEntries = JsonConvert.DeserializeObject<List<DiscussionEntry>>(content) ?? [];
                return discussionEntries;
            }
            else
            {
                Console.WriteLine($"Failed to fetch discussion entries for incidentId: {incidentId}");
                return [];
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

        public async Task<string> AddKeywordToIncident(string incidentId, string keyword)
        {
            if (_icmWorkflowSettings.ReadOnly)
            {
                return "Success. ICM Plugin is in ReadOnly mode.";
            }
            var payload = JsonConvert.SerializeObject(new { incidentId, keyword });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.AddIncidentKeywordsWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                return "Success";
            }
            else
            {
                string errorMessage = $"Failed to add keyword to incident for incidentId: {incidentId}";
                _logger.LogInformation(errorMessage);
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

            // Check to see if the filters have any DateTime column. If there are, then both >= and <= operators must be present.. if Only one is present, throw an exception
            if (filters != null)
            {
                var dateTimeProperties = IncidentAdvancedSearchFilter.GetDateTimeProperties();
                var invalidDateTimeFilters = dateTimeProperties
                    .SelectMany(column =>
                        filters.Where(f => string.Equals(f.ColumnName, column, StringComparison.OrdinalIgnoreCase))
                            .GroupBy(f => f.ColumnName, StringComparer.OrdinalIgnoreCase)
                            .Where(g => (g.Count()%2 > 0) || g.Any(f => f.Operator == ">=" || f.Operator == "<=") &&
                                      !(g.Any(f => f.Operator == ">=") && g.Any(f => f.Operator == "<=")))
                            )
                    .FirstOrDefault();

                if (invalidDateTimeFilters != null)
                {
                    throw new ArgumentException(
                        $"DateTime column '{invalidDateTimeFilters.Key}' must have both '>=' and '<=' operators for proper date range filtering. " +
                        $"Please provide both a lower and upper bound for the date range.");
                }
            }

            // Check if any date-time column appears with both >= and <= operators (date range filter)
            bool hasDateRangeFilter = filters != null &&
                IncidentAdvancedSearchFilter.GetDateTimeProperties()
                .Any(column =>
                    filters.Where(f => string.Equals(f.ColumnName, column, StringComparison.OrdinalIgnoreCase))
                    .GroupBy(f => f.ColumnName, StringComparer.OrdinalIgnoreCase)
                    .Any(g => g.Count() == 2 &&
                        g.Any(f => f.Operator == ">=") && g.Any(f => f.Operator == "<="))
                    );

            // Build the basic Kusto query structure
            var queryBuilder = new StringBuilder();
            if(!hasDateRangeFilter && !(filters?.Any(f => f.ColumnName.Equals("IncidentId", StringComparison.OrdinalIgnoreCase)) == true))
            {
                queryBuilder.AppendLine($"let lookbackPeriod = ago({lookbackPeriodInDays}d);");
            }
            
            queryBuilder.AppendLine($"let resultLimit = {resultLimit};");
            queryBuilder.AppendLine("Incidents");

            // Add the lookback period filter
            if (!hasDateRangeFilter && !(filters?.Any(f => f.ColumnName.Equals("IncidentId", StringComparison.OrdinalIgnoreCase)) == true))
            {
                queryBuilder.AppendLine("| where CreateDate > lookbackPeriod");
            }
            
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
                if (searchResults == null)
                {
                    string errorMessage = $"Failed to deserialize the response of ICMWorkflow request";
                    _logger.LogError(errorMessage);
                    return new List<IncidentAdvancedSearchResultItem> { new IncidentAdvancedSearchResultItem { Id = errorMessage } };

                }
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
        /// <param name="lookbackPeriodInDays">Number of days to look back</param>
        /// <param name="filter3Tuple">List of filters as tuples (ColumnName, Operator, Value)</param>
        /// <param name="resultLimit">Maximum number of results to return</param>
        /// <returns>List of search results matching the criteria</returns>
        public async Task<List<IncidentAdvancedSearchResultItem>> SearchIncidentsWithParametersAsync(
            int lookbackPeriodInDays,
            int resultLimit,
            List<Tuple<string, string, string>> filter3Tuple)
        {
            if (filter3Tuple == null)
            {
                throw new ArgumentNullException(nameof(filter3Tuple), "Filters cannot be null");
            }

            if (filter3Tuple.Count == 0)
            {
                throw new ArgumentException(nameof(filter3Tuple), "Must specify at least one filter expression");
            }

            if (filter3Tuple.Any(f => string.IsNullOrWhiteSpace(f.Item1)) || filter3Tuple.Any(f => string.IsNullOrWhiteSpace(f.Item2)))
            {
                throw new ArgumentException(nameof(filter3Tuple), "Column name and operator cannot be null or empty");
            }

            List<IncidentAdvancedSearchResultItem> result = new List<IncidentAdvancedSearchResultItem>();
            try
            {
                var filters = new List<IncidentAdvancedSearchFilter>();

                foreach (var tupleFilterItem in filter3Tuple)
                {
                    filters.Add(new IncidentAdvancedSearchFilter(tupleFilterItem.Item1, tupleFilterItem.Item2, tupleFilterItem.Item3));
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
                var searchResults = JsonConvert.DeserializeObject<List<SearchItem>>(content) ?? [];
                return searchResults;
            }
            else
            {
                string errorMessage = $"Failed to search incidents with searchString: {searchString}";
                return new List<SearchItem> { new SearchItem { Id = errorMessage } };
            }
        }

        // run query on icm kusto cluster
        public async Task<string> RunKustoQuery(string query)
        {
            var payload = JsonConvert.SerializeObject(new { query });
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.IncidentLookupWorkflowName, payload);
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                return content;
            }
            else
            {
                string errorMessage = $"Failed to run kusto query: {query}";
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

        public async Task<string> TransferIncidentToHumanInterventionAsync(string incidentId, string discussionEntry, string? humanInterventionServiceName = null, string? humanInterventionTeamName = null)
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

        public bool IsEnabled()
        {
            return _icmWorkflowSettings.Enabled;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }
    public class NullableICMWorkflowClient : IICMWorkflowClient
    {
        public void Dispose() { }

        public Task<Incident> GetIncidentAsync(string incidentId) => Task.FromResult<Incident>(new Incident());

        public Task<string> AddAttachmentToIncident(string incidentId, string fileName, string base64Content) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> SetSubscriptionQuota(string subscriptionId, AzureRegion region, string quotaType, string quotaLimit) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId, DateTimeOffset? queryFrom = null) =>
            Task.FromResult(new List<DiscussionEntry>());

        public Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantName, string owningTeam) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> AddTagToIncident(string incidentId, string tag) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> AddKeywordToIncident(string incidentId, string keyword) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<List<IncidentAdvancedSearchResultItem>> SearchIncidentsWithParametersAsync(
            int lookbackPeriodInDays,
            int resultLimit,
            List<Tuple<string, string, string>> filter3Tuple) =>
            Task.FromResult(new List<IncidentAdvancedSearchResultItem>());

        public Task<List<SearchItem>> SearchIncidentsAsync(string searchString, int lookbackPeriodInDays, int resultLimit) =>
            Task.FromResult(new List<SearchItem>());

        public Task<string> DowngradeSeverityAsync(string incidentId, string discussionEntry) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> TransferIncidentToHumanInterventionAsync(string incidentId, string discussionEntry, string? humanInterventionServiceName = null, string? humanInterventionTeamName = null) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> MarkSubFirstPartyAsync(string subscriptionId) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> GetSubDetailsFromGenevaAsync(string subscriptionId) => Task.FromResult<string>(string.Empty);

        public Task<string> RebootWorker(string location, string stampName, string role, string roleInstance) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> GetRedisDeploymentDetailsFromGenevaAsync(string cacheName) => Task.FromResult<string>(string.Empty);

        public Task<string> GetRedisDeploymentHistoryFromGenevaAsync(string cacheName) => Task.FromResult<string>(string.Empty);

        public Task<string> RestartWebApp(string subscriptionId, string webappName, string webspaceName) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<List<CustomField>> GetCustomFieldsAsync(string incidentId) =>
            Task.FromResult(new List<CustomField>());

        public Task<string> GetAppLensDiagnosticsAsync(string incidentId) => Task.FromResult<string>(string.Empty);
        public bool ProcessImages => false;
        public bool IsEnabled() { return false; }
        public Task<string> RunKustoQuery(string query) => Task.FromResult<string>("ICM Plugin is disabled");
    }
    public interface IICMWorkflowClient : IDisposable
    {
        Task<Incident> GetIncidentAsync(string incidentId);
        Task<string> AddAttachmentToIncident(string incidentId, string fileName, string base64Content);
        Task<string> SetSubscriptionQuota(string subscriptionId, AzureRegion region, string quotaType, string quotaLimit);
        Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId, DateTimeOffset? queryFrom = null);
        Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantName, string owningTeam);
        Task<string> AddTagToIncident(string incidentId, string tag);
        Task<string> AddKeywordToIncident(string incidentId, string keyword);
        Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry);
        Task<List<IncidentAdvancedSearchResultItem>> SearchIncidentsWithParametersAsync(
            int lookbackPeriodInDays,
            int resultLimit,
            List<Tuple<string, string, string>> filter3Tuple);
        Task<List<SearchItem>> SearchIncidentsAsync(string searchString, int lookbackPeriodInDays, int resultLimit);
        Task<string> DowngradeSeverityAsync(string incidentId, string discussionEntry);
        Task<string> TransferIncidentToHumanInterventionAsync(string incidentId, string discussionEntry, string? humanInterventionServiceName = null, string? humanInterventionTeamName = null);
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
        bool IsEnabled();
        Task<string> RunKustoQuery(string query);
    }
}

