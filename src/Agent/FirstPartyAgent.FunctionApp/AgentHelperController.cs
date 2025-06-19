using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Agent.Data.DataModels;
using Azure.Identity;
using Microsoft.Bot.Configuration;
using Agent.Core.Models;
using Agent.Core.Configuration;

namespace FirstPartyAgent.FunctionApp;
public class AgentHelperController
{
    private readonly ILogger<AgentHelperController> _logger;
    private readonly OneBranchApprovalServiceSettings _approvalServiceSettings;
    private readonly IChatService _chatService;
    private readonly IAlertProcessingService _alertProcessingService;
    private readonly ISessionMessageService _sessionMessageService;
    private readonly IStorageService _storageService;
    private readonly ICosmosDBService _cosmosDBService;
    private readonly IICMWorkflowClient _icmWorkflowClient;
    private readonly ITeamsClient _teamsClient;
    private readonly TeamsClientSettings _teamsClientSettings;
    private const string hotsiteAgentAlertDetailsCosmosDbContainer = "IcmAlertDetails";
    private readonly AlertHandlerService _alertHandlerService;
    private HttpClient _httpClient;

    public AgentHelperController(
        ILogger<AgentHelperController> logger,
        OneBranchApprovalServiceSettings approvalServiceSettings,
        ICosmosDBService cosmosDBService)
    {
        _logger = logger;
        _approvalServiceSettings = approvalServiceSettings ?? throw new ArgumentNullException(nameof(approvalServiceSettings));

        if (_approvalServiceSettings.Enabled)
        {
            if(string.IsNullOrEmpty(_approvalServiceSettings.ManagedIdentityClientId))
            {
                throw new ArgumentException("ManagedIdentityClientId must be set in ApprovalServiceSettings when ApprovalService is enabled.");
            }
        }
        _cosmosDBService = cosmosDBService;

        _httpClient = new HttpClient();
    }

    [Function("CreateApprovalDocument")]
    public async Task<HttpResponseData> CreateApprovalDocument(
            [HttpTrigger(AuthorizationLevel.Function, "post", Route = "CreateApprovalDocument")] HttpRequestData req)
    {
        HttpResponseData response = null;

        if (!_approvalServiceSettings.Enabled)
        {
            response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("Approval Service is not enabled.");
            return response;
        }

        try
        {
            var body = await req.ReadAsStringAsync();

            if (string.IsNullOrEmpty(body))
            {
                response = req.CreateResponse(HttpStatusCode.BadRequest);
                await response.WriteStringAsync("Request body cannot be empty.");
                return response;
            }

            var credential = new ManagedIdentityCredential(clientId: _approvalServiceSettings.ManagedIdentityClientId);
            var scopes = new[] { _approvalServiceSettings.Resource };
            var token = await credential.GetTokenAsync(new Azure.Core.TokenRequestContext(scopes));

            var baseUrl = new Uri(_approvalServiceSettings.Endpoint);
            var url = new Uri(baseUrl, "api/CreateApprovalDocumentV2");

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

            var httpResponse = await _httpClient.SendAsync(request);
            var content = await httpResponse.Content.ReadAsStringAsync();

            if(!httpResponse.IsSuccessStatusCode)
            {
                throw new Exception($"http {httpResponse.StatusCode} - {content}");
            }

            response = req.CreateResponse(httpResponse.StatusCode);
            response.Headers.Add("Content-Type", "application/json");
            await response.WriteStringAsync(content);
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating approval document.");
            response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error creating approval document: {ex.Message}");
            return response;
        }
    }

    [Function("GetApprovalRequest")]
    public async Task<HttpResponseData> GetApprovalRequest(
            [HttpTrigger(AuthorizationLevel.Function, "get", Route = "GetApprovalRequest/{approvalId}")] HttpRequestData req,
            string approvalId)
    {
        HttpResponseData response = null;
        if (!_approvalServiceSettings.Enabled)
        {
            response = req.CreateResponse(HttpStatusCode.BadRequest);
            await response.WriteStringAsync("Approval Service is not enabled.");
            return response;
        }
        try
        {

            var query = _cosmosDBService.GetQueryableContainer<OneBranchApprovalStatus>("IcmAgent", "ApprovalRequest");

            var request = await query.Where(x => x.Data.ApprovalDocumentId == approvalId).ToListAsync();

            if (!request.Any())
            {
                response = req.CreateResponse(HttpStatusCode.NotFound);
                await response.WriteStringAsync($"Approval request with ID {approvalId} not found.");
                return response;
            }

            response = req.CreateResponse(HttpStatusCode.OK);
            await response.WriteAsJsonAsync(request[0]);
            return response;

        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting approval request.");
            response = req.CreateResponse(HttpStatusCode.InternalServerError);
            await response.WriteStringAsync($"Error getting approval request: {ex.Message}");
            return response;
        }
    }

}
