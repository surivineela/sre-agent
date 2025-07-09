// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Security.Cryptography.X509Certificates;
using System.Text;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Models.ICM;
using Agent.Core.Services;
using Agent.Logging;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OperationalAgent.Core.Extensions;
using Newtonsoft.Json;

namespace Agent.Plugins.IcmPlugin
{
    public class ICMWorkflowClient : IICMWorkflowClient
    {
        private readonly bool IsDevelopment;
        private static Lazy<Task<HttpClient>>? _httpClientLazy;
        private readonly ILogger<ICMWorkflowClient> _logger;
        private readonly ICMWorkflowSettings _icmWorkflowSettings;
        private readonly IKeyVaultService _keyVaultService;
        private const string ActionPath = "triggers/manual/execute";
        private readonly int TimeoutInSeconds = 600;
        private bool _processImages = true;
        public bool ProcessImages => _processImages;

        public ICMWorkflowClient(IHostEnvironment environment, ILogger<ICMWorkflowClient> logger, ICMWorkflowSettings icmWorkflowSettings, IKeyVaultService keyVaultService)
        {
            _icmWorkflowSettings = icmWorkflowSettings;
            _processImages = _icmWorkflowSettings.ProcessImages;
            IsDevelopment = environment.IsDevelopment();
            _logger = logger;
            _keyVaultService = keyVaultService;

            if (!icmWorkflowSettings.Enabled)
            {
                return;
            }

            _httpClientLazy = new Lazy<Task<HttpClient>>(() => InitializeHttpClientAsync(icmWorkflowSettings, keyVaultService, environment.IsDevelopment(), logger));

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

        }

        private static async Task<HttpClient> InitializeHttpClientAsync(ICMWorkflowSettings icmWorkflowSettings, IKeyVaultService userKeyVaultService, bool isDevelopment, ILogger<ICMWorkflowClient> logger)
        {
            const int TimeoutInSeconds = 600;

            if (icmWorkflowSettings.UseFunctionApp)
            {
                var httpClient = new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                };
                httpClient.DefaultRequestHeaders.Add("x-functions-key", icmWorkflowSettings.FunctionAppKey);
                return httpClient;
            }
            else
            {
                if (isDevelopment && !string.IsNullOrWhiteSpace(icmWorkflowSettings.UserToken))
                {
                    var httpClient = new HttpClient()
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };
                    httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {icmWorkflowSettings.UserToken}");
                    return httpClient;
                }

                if (userKeyVaultService.IsEnabled)
                {
                    string certificateKeyVaultSecretName = string.Empty;
                    var settingName = "ICMWorkflowSettings--CertificateKeyVaultSecretName";
                    try
                    {
                        logger.LogInternalInformation($"KeyVaultService is enabled. Attempting to read {settingName} from KeyVault {userKeyVaultService.KeyVaultUri}.");
                        certificateKeyVaultSecretName = await userKeyVaultService.ReadSecretAsync(settingName);
                    }
                    catch (Azure.RequestFailedException ex)
                    {
                        logger.LogInternalError(ex, $"Error reading {settingName} from KeyVault. Continue without using the users certificate.");
                    }

                    if (!string.IsNullOrWhiteSpace(certificateKeyVaultSecretName))
                    {
                        var certificate = CertLoader.LoadCertFromKeyVault(
                            KeyVaultConfigurationExtension.GetPlatformKeyVaultSettingFromEnvironment("KeyVaultUri"),
                            certificateKeyVaultSecretName,
                            KeyVaultConfigurationExtension.GetPlatformKeyVaultSettingFromEnvironment("Identity"),
                        null,
                        logger);

                        var handler = new HttpClientHandler();
                        handler.ClientCertificates.Add(certificate);
                        logger.LogExternalInformation("Successfully loaded Cert from keyvault for ICMWorkflowClient.");
                        return new HttpClient(handler)
                        {
                            Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                        };
                    }
                }


                if ((!string.IsNullOrWhiteSpace(icmWorkflowSettings.CertificateSubjectName) || !string.IsNullOrEmpty(icmWorkflowSettings.CertificateKeyVaultSecretName)) && (!string.IsNullOrWhiteSpace(icmWorkflowSettings.CertificateKeyVaultUri)))
                {
                    string certKvSecretName = icmWorkflowSettings.CertificateKeyVaultSecretName;

                    string keyVaultUri = icmWorkflowSettings.CertificateKeyVaultUri;

                    string certMsi = icmWorkflowSettings.ManagedIdentityClientId ?? string.Empty;

                    logger.LogInternalInformation($"Loading cert from Key Vault: {keyVaultUri}, Certificate Name: {certKvSecretName}, Managed Identity Client Id: {certMsi}");
                    var certificate = CertLoader.LoadCertFromKeyVault(keyVaultUri, certKvSecretName, certMsi, null, logger);

                    var handler = new HttpClientHandler();
                    handler.ClientCertificates.Add(certificate);
                    logger.LogExternalInformation("Successfully loaded Cert from keyvault for ICMWorkflowClient.");
                    return new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };

                }
                else if (!string.IsNullOrWhiteSpace(icmWorkflowSettings.CertificateKeyVaultUri) && !string.IsNullOrEmpty(icmWorkflowSettings.CertificateKeyVaultSecretName))
                {
                    var handler = new HttpClientHandler();
                    var certificate = CertLoader.LoadCertFromKeyVault(icmWorkflowSettings.CertificateKeyVaultUri, icmWorkflowSettings.CertificateKeyVaultSecretName, icmWorkflowSettings.ManagedIdentityClientId, null, logger);
                    handler.ClientCertificates.Add(certificate);
                    logger.LogExternalInformation("Successfully loaded Cert from keyvault for ICMWorkflowClient.");
                    return new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };
                }
                else if (!string.IsNullOrWhiteSpace(icmWorkflowSettings.CertificateFilePath))
                {
                    var handler = new HttpClientHandler();
                    var certificate = CertLoader.LoadCertFromFile(icmWorkflowSettings.CertificateFilePath);
                    handler.ClientCertificates.Add(certificate);
                    logger.LogExternalInformation("Successfully loaded Cert file for ICMWorkflowClient.");
                    return new HttpClient(handler)
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
                        var certificates = store.Certificates.Find(X509FindType.FindBySubjectName, icmWorkflowSettings.CertificateSubjectName, validOnly: false);
                        if (certificates == null || certificates.Count == 0)
                        {
                            throw new Exception($"Certificate with subject matching '{icmWorkflowSettings.CertificateSubjectName}' not found.");
                        }

                        // Use the first matching certificate.
                        handler.ClientCertificates.Add(certificates[0]);
                    }

                    return new HttpClient(handler)
                    {
                        Timeout = TimeSpan.FromSeconds(TimeoutInSeconds)
                    };
                }
            }
        }

        public async Task<HttpResponseMessage> SendICMWorkflowRequest(string workflowName, string body, string tenantId = null)
        {
            var httpClient = await _httpClientLazy!.Value;
            _logger.LogExternalInformation($"Sending ICM Workflow Request. WorkflowName: {workflowName}, Body: {body}");
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
                    var response = await httpClient.PostAsync(requestUri, content);
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
                    _logger.LogExternalInformation("Sending request to ICM Workflow API: {requestUri}", requestUri);
                    var response = await httpClient.PostAsync(requestUri, content);
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
                return incident;
            }
            else
            {
                throw new Exception($"Failed to fetch incident info for incidentId: {incidentId}");
            }
        }

        public async Task<SubscriptionDetail> GetSubscriptionDetail(string subscriptionId)
        {
            var response = await SendICMWorkflowRequest(_icmWorkflowSettings.SubscriptionDetailWorkflowName, JsonConvert.SerializeObject(new { SubscriptionId = subscriptionId }));
            if (response.IsSuccessStatusCode)
            {
                var content = await response.Content.ReadAsStringAsync();
                var result = JsonConvert.DeserializeObject<SubscriptionDetail>(content);
                _logger.LogExternalInformation($"GetSubscriptionDetail Completed. The subscription OfferType is {result?.OfferType}, QuotaId is {result?.QuotaId}");
                return result;
            }
            _logger.LogInternalError($"Failed to fetch SubscriptionDetail for subscription {subscriptionId}, statusCode: {response.StatusCode}");
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
            _logger.LogInternalError($"Failed to fetch SubscriptionUsage for subscription {subscriptionId}, statusCode: {response.StatusCode}");
            return null;
        }

        public async Task<string> GetSubscriptionQuota(string subscriptionId, string region, string quotaType)
        {
            const string workflowName = "Workflow-GenevaAction-GetSubscriptionQuota";
            Dictionary<string, string> body = new()
            {
                { "SubscriptionId", subscriptionId },
                { "Region", region },
                { "QuotaType", quotaType },
            };
            var response = await SendICMWorkflowRequest(workflowName, JsonConvert.SerializeObject(body));
            if (response.IsSuccessStatusCode) { return await response.Content.ReadAsStringAsync(); }
            return $"Failed to get subscription quota with {response.StatusCode} error: {await response.Content.ReadAsStringAsync()}";
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

        public async Task<string> GetEnvironmentQuota(string environmentUrl, string region, string quotaType)
        {
            const string workflowName = "Workflow-GenevaAction-GetEnvironmentQuota";
            Dictionary<string, string> body = new()
            {
                { "EnvironmentURL", environmentUrl},
                { "Region", region },
                { "QuotaType", quotaType },
            };
            var response = await SendICMWorkflowRequest(workflowName, JsonConvert.SerializeObject(body));
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadAsStringAsync();
            }
            return $"Failed to get environment quota with {response.StatusCode} error: {await response.Content.ReadAsStringAsync()}";
        }

        public async Task<string> SetEnvironmentQuota(string incidentId, string environmentUrl, string region, string quotaType, string quotaLimit)
        {
            const string workflowName = "Workflow-GenevaAction-SetEnvironmentQuota";
            Dictionary<string, string> body = new()
            {
                { "IncidentId", incidentId },
                { "EnvironmentURL", environmentUrl},
                { "Region", region },
                { "QuotaType", quotaType },
                { "QuotaLimit", quotaLimit },
            };
            var response = await SendICMWorkflowRequest(workflowName, JsonConvert.SerializeObject(body));
            var responseContent = await response.Content.ReadAsStringAsync();
            if (response.IsSuccessStatusCode)
            {
                return responseContent;
            }
            else
            {
                if (!string.IsNullOrEmpty(responseContent))
                {
                    return $"Failed to set environment quota with {response.StatusCode} error: {responseContent}";
                }
                else
                {
                    return $"Failed to set environment quota with unknow error. Please check Workflow-GenevaAction-SetEnvironmentQuota run history in https://portal.microsofticm.com/imp/v5/automation/workflows/Workflow-GenevaAction-SetEnvironmentQuota.";
                }
            }
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
                var discussionEntries = JsonConvert.DeserializeObject<List<DiscussionEntry>>(content);
                return discussionEntries;
            }
            else
            {
                Console.WriteLine($"Failed to fetch discussion entries for incidentId: {incidentId}");
                return null;
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

        public bool IsEnabled()
        {
            return _icmWorkflowSettings.Enabled;
        }

        public void Dispose()
        {
            if (_httpClientLazy?.IsValueCreated == true)
            {
                var httpClientTask = _httpClientLazy.Value;
                if (httpClientTask.IsCompletedSuccessfully)
                {
                    httpClientTask.Result?.Dispose();
                }
            }
        }
    }
    public class NullableICMWorkflowClient : IICMWorkflowClient
    {
        public void Dispose() { }

        public Task<Incident> GetIncidentAsync(string incidentId) => Task.FromResult<Incident>(null);

        public Task<SubscriptionDetail> GetSubscriptionDetail(string subscriptionId) => Task.FromResult<SubscriptionDetail>(null);

        public Task<AcaSubscriptionUsage> GetSubscriptionUsage(string subscriptionId) => Task.FromResult<AcaSubscriptionUsage>(null);

        public Task<string> GetSubscriptionQuota(string subscriptionId, string region, string quotaType) =>
            Task.FromResult("ICM Plugin is disabled");
        public Task<string> SetSubscriptionQuota(string subscriptionId, string region, string quotaType, string quotaLimit) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> GetEnvironmentQuota(string environmentUrl, string region, string quotaType) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> SetEnvironmentQuota(string incidentId, string environmentUrl, string region, string quotaType, string quotaLimit) =>
            Task.FromResult("ICM Plugin is disabled");
        public Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId, DateTimeOffset? queryFrom = null) =>
            Task.FromResult(new List<DiscussionEntry>());

        public Task<string> AddTagToIncident(string incidentId, string tag) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry) =>
            Task.FromResult("ICM Plugin is disabled");

        public Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry) =>
            Task.FromResult("ICM Plugin is disabled");

        public bool ProcessImages => false;
        public bool IsEnabled() { return false; }

        public Task<HttpResponseMessage> SendICMWorkflowRequest(string workflowName, string body, string tenantId = null) =>
            Task.FromResult(new HttpResponseMessage(System.Net.HttpStatusCode.NotImplemented));
    }
    public interface IICMWorkflowClient : IDisposable
    {
        Task<HttpResponseMessage> SendICMWorkflowRequest(string workflowName, string body, string tenantId = null);
        Task<Incident> GetIncidentAsync(string incidentId);
        Task<SubscriptionDetail> GetSubscriptionDetail(string subscriptionId);
        Task<AcaSubscriptionUsage> GetSubscriptionUsage(string subscriptionId);
        Task<string> GetSubscriptionQuota(string subscriptionId, string region, string quotaType);
        Task<string> SetSubscriptionQuota(string subscriptionId, string region, string quotaType, string quotaLimit);
        Task<string> GetEnvironmentQuota(string environmentUrl, string region, string quotaType);
        Task<string> SetEnvironmentQuota(string incidentId, string environmentUrl, string region, string quotaType, string quotaLimit);
        Task<List<DiscussionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId, DateTimeOffset? queryFrom = null);
        Task<string> AddTagToIncident(string incidentId, string tag);
        Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry);
        Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry);
        Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry);
        bool ProcessImages { get; }
        bool IsEnabled();
    }
}
