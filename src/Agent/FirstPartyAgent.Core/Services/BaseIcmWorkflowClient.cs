// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.Logging;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Hosting;
using Newtonsoft.Json;
using System.ComponentModel;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using FirstPartyAgent.Core.Configuration;

namespace FirstPartyAgent.Core.Services
{
    public class BaseIcmWorkflowClient: IBaseIcmWorkflowClient
    {
        private readonly bool IsDevelopment;
        private static HttpClient _httpClient;
        private readonly ILogger<BaseIcmWorkflowClient> _logger;
        private readonly BaseIcmWorkflowSettings _icmWorkflowSettings;
        private const string ActionPath = "triggers/manual/execute";
        private readonly int TimeoutInSeconds = 600;
        private readonly bool _readOnly = false;
        public bool ReadOnly => _readOnly;

        public BaseIcmWorkflowClient(IHostEnvironment environment, ILogger<BaseIcmWorkflowClient> logger, ICMWorkflowSettings icmWorkflowSettings)
        {
            if (!icmWorkflowSettings.Enabled)
            {
                return;
            }
            _icmWorkflowSettings = JsonConvert.DeserializeObject<BaseIcmWorkflowSettings>(JsonConvert.SerializeObject(icmWorkflowSettings));
            _readOnly = _icmWorkflowSettings.ReadOnly;
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
                if (!IsDevelopment && string.IsNullOrWhiteSpace(_icmWorkflowSettings.CertificateSubjectName))
                {
                    throw new Exception("The environment variable 'ICMWorkflows:CertificateSubjectName' is not set.");
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

        public async Task<HttpResponseMessage> SendICMWorkflowRequest(string workflowName, string body, string tenantId = null)
        {
            _logger.LogInformation($"Sending ICM Workflow Request. WorkflowName: {workflowName}, Body: {body}");
            if (string.IsNullOrWhiteSpace(workflowName))
                throw new ArgumentException("Workflow name must be provided.", nameof(workflowName));
            if (string.IsNullOrWhiteSpace(body))
                throw new ArgumentException("Body must be provided.", nameof(body));
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                throw new ArgumentException("tenantId must be provided.", nameof(body));
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
                    var response = await _httpClient.PostAsync(requestUri, content);
                    return response;
                }
            }
        }

        public async Task<HttpResponseMessage> ExecuteGetCallsInICMWorkflowsFunctionApp(string apiPath)
        {
            if (string.IsNullOrWhiteSpace(_icmWorkflowSettings.FunctionAppEndpoint))
            {
                throw new Exception("'ICMWorkflows:FunctionAppEndpoint' is not set in the configuration");
            }

            var response = await _httpClient.GetAsync($"{_icmWorkflowSettings.FunctionAppEndpoint}{apiPath}");
            return response;
        }

        public void Dispose()
        {
            _httpClient?.Dispose();
        }
    }

    public interface IBaseIcmWorkflowClient : IDisposable
    {
        bool ReadOnly { get; }
        Task<HttpResponseMessage> SendICMWorkflowRequest(string workflowName, string body, string tenantId = null);
        Task<HttpResponseMessage> ExecuteGetCallsInICMWorkflowsFunctionApp(string apiPath);
    }

    /// <summary>
    /// Nullable implementation of IBaseIcmWorkflowClient that performs no-ops and returns null/defaults.
    /// </summary>
    public class NullableBaseIcmWorkflowClient : IBaseIcmWorkflowClient
    {
        public bool ReadOnly => true;

        public Task<HttpResponseMessage> SendICMWorkflowRequest(string workflowName, string body, string tenantId = null)
        {
            return Task.FromResult<HttpResponseMessage>(null);
        }

        public Task<HttpResponseMessage> ExecuteGetCallsInICMWorkflowsFunctionApp(string apiPath)
        {
            return Task.FromResult<HttpResponseMessage>(null);
        }

        public void Dispose()
        {
            // No resources to dispose.
        }
    }
}

