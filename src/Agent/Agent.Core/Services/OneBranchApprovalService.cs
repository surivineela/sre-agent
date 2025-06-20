using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Agent.Logging;
using Agent.Core.Models;
using Azure.Identity;
using System.Net.Http;
using System.Net.Http.Headers;
using Azure.Core;
using System.Diagnostics;

namespace Agent.Core.Services;
public class OneBranchApprovalService
{
    private ILogger _logger;
    private AgentHelperSettings _agentHelperSettings;
    private OneBranchApprovalServiceSettings _oneBranchApprovalServiceSettings;
    private HttpClient _httpClient;

    public bool IsEnabled = false;

    private TimeSpan maxPollingTime = TimeSpan.FromDays(7); 
    private TimeSpan initialDelay = TimeSpan.FromMinutes(1);
    private TimeSpan maxDelay = TimeSpan.FromHours(2);

    const string createApprovalDocApi = "api/ApprovalService/CreateApprovalDocument";
    const string getApprovalRequestApi = "api/ApprovalService/GetApprovalRequest";


    public OneBranchApprovalService(
        ILogger<OneBranchApprovalService> logger,
        AgentHelperSettings agentHelperSettings,
        OneBranchApprovalServiceSettings oneBranchApprovalServiceSettings)
    {
        _logger = logger;
        _agentHelperSettings = agentHelperSettings ?? throw new ArgumentNullException(nameof(agentHelperSettings));
        _oneBranchApprovalServiceSettings = oneBranchApprovalServiceSettings ?? throw new ArgumentNullException(nameof(oneBranchApprovalServiceSettings));

        if (!_agentHelperSettings.Enabled)
        {
            if (!_oneBranchApprovalServiceSettings.Enabled)
            {
                _logger.LogInternalInformation("OneBranchApprovalService is disabled.");
                return;
            }
            else
            {
                throw new ArgumentException("AgentHelperSettings must be enabled when OneBranchApprovalService is enabled.");
            }
        }

        IsEnabled = true;


        if (Debugger.IsAttached)
        {
            var options = new DefaultAzureCredentialOptions
            {
                ExcludeEnvironmentCredential = false,
                ExcludeManagedIdentityCredential = true,
                ExcludeWorkloadIdentityCredential = false,
                ExcludeSharedTokenCacheCredential = false,
                ExcludeVisualStudioCredential = false,
                ExcludeVisualStudioCodeCredential = false,
                ExcludeAzureCliCredential = false,
                ExcludeInteractiveBrowserCredential = false,
                ExcludeAzurePowerShellCredential = false
            };

            var credential = new DefaultAzureCredential(options);
            _httpClient = new HttpClient(new TokenCredentialHttpClientHandler(credential, _agentHelperSettings.Resource));
        }
        else
        {

            if (string.IsNullOrEmpty(_agentHelperSettings.ManagedIdentityClientId))
            {
                throw new ArgumentException("ManagedIdentityClientId must be set in AgentHelperSettings.");
            }

            var managedIdentityCredential = new ManagedIdentityCredential(_agentHelperSettings.ManagedIdentityClientId);

            _httpClient = new HttpClient(new TokenCredentialHttpClientHandler(managedIdentityCredential, _agentHelperSettings.Resource));
        }

        if (!string.IsNullOrEmpty(_agentHelperSettings.Endpoint))
        {
            _httpClient.BaseAddress = new Uri(_agentHelperSettings.Endpoint);
        }
        else
        {
            throw new ArgumentException("Endpoint must be set in AgentHelperSettings.");
        }
    }


    public async Task<OneBranchApprovalResponse> CreateApprovalDocumentAsync(OneBranchApprovalRequest request)
    {
        if (request == null)
        {
            throw new ArgumentNullException(nameof(request), "Approval request cannot be null.");
        }
        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(createApprovalDocApi, content);
        if (!response.IsSuccessStatusCode)
        {
            var respContent = await response.Content.ReadAsStringAsync();
            _logger.LogInternalError("Failed to create approval document. Status code: {StatusCode}, Reason: {ReasonPhrase}, Content: {Content}",
                response.StatusCode, response.ReasonPhrase, respContent);

            throw new Exception($"Failed to create approval document. Status code: {response.StatusCode}, Reason: {response.ReasonPhrase}.");
        }
        string json = await response.Content.ReadAsStringAsync();

        var resp = JsonConvert.DeserializeObject<OneBranchApprovalResponse>(json);

        if (resp == null)
        {
            _logger.LogInternalError($"Failed to deserialize response from CreateApprovalDocument: {json}");
            throw new Exception("Failed to deserialize response from CreateApprovalDocument.");
        }

        _logger.LogInternalInformation("Approval document created successfully with ID: {ApprovalId}", resp.ApprovalDocumentId);
        return resp;
    }

    public async Task<OneBranchApprovalStatus> GetApprovalRequestAsync(string id)
    {
        if (string.IsNullOrEmpty(id))
        {
            throw new ArgumentException("Approval ID cannot be null or empty.", nameof(id));
        }
        var response = await _httpClient.GetAsync($"{getApprovalRequestApi}/{id}");
        if (!response.IsSuccessStatusCode)
        {
            if(response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                _logger.LogInternalWarning("Approval request with ID {ApprovalId} not found.", id);
                return null; // or throw a specific exception if needed
            }

            _logger.LogInternalError("Failed to get approval request. Status code: {StatusCode}, Reason: {ReasonPhrase}",
                response.StatusCode, response.ReasonPhrase);
            throw new Exception("Failed to get approval request.");
        }
        string json = await response.Content.ReadAsStringAsync();
        var approvalStatus = JsonConvert.DeserializeObject<OneBranchApprovalStatus>(json);
        if (approvalStatus == null)
        {
            _logger.LogInternalError("Failed to deserialize response from GetApprovalRequest.");
            throw new Exception("Failed to get approval request.");
        }
        _logger.LogInternalInformation("Retrieved approval request with ID: {ApprovalId}", id);
        return approvalStatus;
    }

    public async Task<OneBranchApprovalStatus> PollForApprovalAsync(string approvalId)
    {
        var startTime = DateTime.UtcNow;
        var currentDelay = initialDelay;

        while (DateTime.UtcNow - startTime < maxPollingTime)
        {
            try
            {
                var logMessage = $"[poll_for_approval][{DateTime.UtcNow}] Checking approval status for ID: {approvalId}. Current delay: {currentDelay.TotalMinutes:F1} minutes.";
                _logger.LogInternalInformation(logMessage);

                var approvalStatus = await GetApprovalRequestAsync(approvalId);

                if (approvalStatus != null)
                {
                    string action = approvalStatus?.Data?.ApprovalDocumentCompleteDetails?.Action;
                    logMessage = $"[poll_for_approval][{DateTime.UtcNow}] Approval process completed. Status: {action}";
                    _logger.LogInternalInformation(logMessage);
                    return approvalStatus;
                }

                logMessage = $"[poll_for_approval][{DateTime.UtcNow}] Approval still pending. Waiting {currentDelay.TotalMinutes:F1} minutes before next check.";
                _logger.LogInternalInformation(logMessage);

                await Task.Delay(currentDelay);

                currentDelay = TimeSpan.FromMilliseconds(Math.Min(currentDelay.TotalMilliseconds * 1.5, maxDelay.TotalMilliseconds));
            }
            catch (Exception ex)
            {
                var errorMessage = $"[poll_for_approval][{DateTime.UtcNow}] Error checking approval status: {ex.Message}. Retrying in {currentDelay.TotalMinutes:F1} minutes.";
                _logger.LogInternalError(errorMessage);
                throw new Exception(errorMessage, ex);
            }
        }

        // Timeout reached
        throw new TimeoutException($"Approval polling timed out after {maxPollingTime.TotalHours} hours for approval ID: {approvalId}");
    }


    private class TokenCredentialHttpClientHandler : HttpClientHandler
    {
        private readonly TokenCredential _credential;
        private readonly string _scope;

        public TokenCredentialHttpClientHandler(TokenCredential credential, string scope)
        {
            _credential = credential ?? throw new ArgumentNullException(nameof(credential));
            _scope = scope ?? throw new ArgumentNullException(nameof(scope));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var tokenRequestContext = new TokenRequestContext(new[] { _scope + "/.default" });
            var accessToken = await _credential.GetTokenAsync(tokenRequestContext, cancellationToken);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken.Token);
            return await base.SendAsync(request, cancellationToken);
        }
    }

}

