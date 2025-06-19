using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Net;
using System.Security.Claims; // For accessing user claims
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Models;
using Azure.Identity;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Models;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Helper.Models;
using FirstPartyAgent.Helper.Services; // For IApprovalAuditEventLogger and ApprovalAuditEvent
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace FirstPartyAgent.Helper;

[Authorize]
[ApiController]
[Route("api/[controller]")]
public class ApprovalServiceController : ControllerBase
{
    private readonly ILogger<ApprovalServiceController> _logger;
    private readonly OneBranchApprovalServiceSettings _approvalServiceSettings;
    private readonly ICosmosDBService _cosmosDBService;
    private readonly IApprovalAuditEventLogger _auditLogger;
    private readonly IConfiguration _config;
    private HttpClient _httpClient;

    public ApprovalServiceController(
        ILogger<ApprovalServiceController> logger,
        OneBranchApprovalServiceSettings approvalServiceSettings,
        ICosmosDBService cosmosDBService,
        IApprovalAuditEventLogger auditLogger,
        IConfiguration config)
    {
        _logger = logger;
        _approvalServiceSettings = approvalServiceSettings ?? throw new ArgumentNullException(nameof(approvalServiceSettings));
        _auditLogger = auditLogger ?? throw new ArgumentNullException(nameof(auditLogger)); // Initialize
        _config = config;
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

    [HttpPost("CreateApprovalDocument")]
    public async Task<IActionResult> CreateApprovalDocument([FromBody] OneBranchApprovalRequest approvalRequest)
    {
        if (!_approvalServiceSettings.Enabled)
        {
            return BadRequest("Approval Service is not enabled.");
        }

        try
        {
            if (approvalRequest == null)
            {
                return BadRequest("Request body cannot be empty.");
            }

            var credential = new ManagedIdentityCredential(clientId: _approvalServiceSettings.ManagedIdentityClientId);
            var scopes = new[] { _approvalServiceSettings.Resource };
            var token = await credential.GetTokenAsync(new Azure.Core.TokenRequestContext(scopes));

            var baseUrl = new Uri(_approvalServiceSettings.Endpoint);
            var url = new Uri(baseUrl, "api/CreateApprovalDocumentV2");

            var request = new HttpRequestMessage(HttpMethod.Post, url)
            {
                Content = new StringContent(JsonConvert.SerializeObject(approvalRequest), Encoding.UTF8, "application/json")
            };

            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.Token);

            var httpResponse = await _httpClient.SendAsync(request);
            var content = await httpResponse.Content.ReadAsStringAsync();

            if(!httpResponse.IsSuccessStatusCode)
            {
                throw new Exception($"http {httpResponse.StatusCode} - {content}");
            }

            var approvalResponse = JsonConvert.DeserializeObject<OneBranchApprovalResponse>(content);
            var auditEvent = new ApprovalCreationRequestAuditEvent
            {
                AuditTime = DateTime.UtcNow,
                CorrelationId = approvalRequest.CorrelationId,
                OperationId = approvalResponse.OperationId,
                ApprovalDocumentId = approvalResponse.ApprovalDocumentId,
                ReleaseApproversAllowed = approvalRequest.ReleaseApproversAllowed,
                Title = approvalRequest.Title,
                RequestDescription = approvalRequest.RequestDescription,
                Submitter = approvalRequest.Submitter,
                ServiceTreeGuid = approvalRequest.ServiceTreeGuid,
            };

            await _auditLogger.LogEventAsync(auditEvent);


            return new ContentResult
            {
                Content = content,
                ContentType = "application/json",
                StatusCode = (int)httpResponse.StatusCode
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating approval document.");
            return StatusCode((int)HttpStatusCode.InternalServerError, $"Error creating approval document: {ex.Message}");
        }
    }

    [HttpGet("GetApprovalRequest/{approvalId}")]
    public async Task<IActionResult> GetApprovalRequest(string approvalId)
    {
        if (!_approvalServiceSettings.Enabled)
        {
            return BadRequest("Approval Service is not enabled.");
        }
        try
        {
            var query = _cosmosDBService.GetQueryableContainer<OneBranchApprovalStatus>("IcmAgent", "ApprovalRequest");

            var requestItems = await query.Where(x => x.Data.ApprovalDocumentId == approvalId).ToListAsync();

            if (!requestItems.Any())
            {
                return NotFound($"Approval request with ID {approvalId} not found.");
            }

            var auditEvent = new ApprovalActionAuditEvent
            {
                AuditTime = DateTime.UtcNow,
                OperationId = requestItems[0].Id,
                ApprovalDocumentId = requestItems[0].Data.ApprovalDocumentId,
                CorrelationId = requestItems[0].Data.CorrelationId,
                Principal = requestItems[0].Data.ApprovalDocumentCompleteDetails.Principal,
                Action = requestItems[0].Data.ApprovalDocumentCompleteDetails.Action,
                Comments = requestItems[0].Data.ApprovalDocumentCompleteDetails.Comments,
                EventTime = requestItems[0].EventTime,
                Topic = requestItems[0].Topic,
                Subject = requestItems[0].Subject,
                EventType = requestItems[0].EventType
            };
            await _auditLogger.LogEventAsync(auditEvent);

            return Ok(requestItems[0]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting approval request.");
            return StatusCode((int)HttpStatusCode.InternalServerError, $"Error getting approval request: {ex.Message}");
        }
    }

    [HttpGet("ok")]
    public async Task<IActionResult> OkFunction()
    {
        return Ok();
    }

}
