using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using Newtonsoft.Json;

namespace Agent.Core.Services;
public class AgentHelperService
{
    public bool IsEnabled => _agentHelperSettings.Enabled;

    private readonly AgentHelperSettings _agentHelperSettings;
    private readonly IAuthenticationService _authenticationService;
    private readonly HttpClient? _httpClient;

    const string createApprovalDocApi = "api/ApprovalService/CreateApprovalDocument";
    const string getApprovalRequestApi = "api/ApprovalService/GetApprovalRequest";
    const string getAzureAlertingDetailsApi = "api/AzureAlerting/GetByTeamId";

    public AgentHelperService(AgentHelperSettings agentHelperSettings, IAuthenticationService authenticationService)
    {
        _agentHelperSettings = agentHelperSettings ?? throw new ArgumentNullException(nameof(agentHelperSettings));
        _authenticationService = authenticationService ?? throw new ArgumentNullException(nameof(authenticationService));

        if (_agentHelperSettings.Enabled)
        {
            _httpClient = new HttpClient(new TokenCredentialHttpClientHandler(_authenticationService.GetAgentHelperCredential(), _agentHelperSettings.Resource));
            _httpClient.BaseAddress = new Uri(_agentHelperSettings.Endpoint);
        }
        else
        {
            _httpClient = null;
        }
    }

    public async Task<HttpResponseMessage> GetApprovalRequestAsync(string id)
    {
        if (!IsEnabled || _httpClient == null)
        {
            throw new InvalidOperationException("AgentHelperService is not enabled.");
        }
        var response = await _httpClient.GetAsync($"{getApprovalRequestApi}/{id}");

        return response;
    }

    public async Task<HttpResponseMessage> CreateApprovalDocumentAsync(OneBranchApprovalRequest request)
    {
        if (!IsEnabled || _httpClient == null)
        {
            throw new InvalidOperationException("AgentHelperService is not enabled.");
        }

        var content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
        var response = await _httpClient.PostAsync(createApprovalDocApi, content);

        return response;
    }

    public async Task<HttpResponseMessage> GetAzureAlertingDetailsAsync(int teamId)
    {
        if (!IsEnabled || _httpClient == null)
        {
            throw new InvalidOperationException("AgentHelperService is not enabled.");
        }
        var response = await _httpClient.GetAsync($"{getAzureAlertingDetailsApi}/{teamId}");
        return response;
    }
}
