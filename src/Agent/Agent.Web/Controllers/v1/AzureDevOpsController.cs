using System.ComponentModel.DataAnnotations;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Graph.Crawler.ARM;
using Agent.Plugins.Interface;
using Agent.Plugins.Services;
using Agent.Web.Authorization;
using Microsoft.AspNetCore.Mvc;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class AzureDevOpsController(
    ILogger<AzureDevOpsController> _logger,
    IThreadRepository _threadRepository,
    IAzureDevOpsWorkItemPlugin _azureDevOpsWorkItemPlugin,
    IGraphDatabaseClient _graphDbClient,
    ICrawlerTriggerService _crawlerTriggerService,
    JwtValidationHelper _jwtValidationHelper) : ControllerBase
{
    [HttpGet("auth/start")]
#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation
    public async Task<IActionResult> StartAzureDevOpsAuth([FromQuery] string resourceId)
#pragma warning restore CUSTOM004 // HTTP action must declare AuthorizeArmOperation
    {
        try
        {
            // Get and persist token.
            AzureDevOpsAccessToken authToken = await _azureDevOpsWorkItemPlugin.GetToken();
            var token = await _threadRepository.CreateOrUpdateAzureDevOpsAccessTokenAsync(new(authToken.AccessToken, ExpiresOn: authToken.ExpiresOn), resourceId);
            var htmlResponse = @"
            <!DOCTYPE html>
            <html lang='en'>
            <head>
                <meta charset='UTF-8'>
                <meta name='viewport' content='width=device-width, initial-scale=1.0'>
                <title>Azure DevOps Authentication Complete</title>
                <style>
                    body {
                        font-family: Arial, sans-serif;
                        text-align: left;
                        margin-top: 50px;
                    }
                    h1 {
                        color:rgb(0, 0, 0);
                    }
                    p {
                        font-size: 16px;
                        color: #555;
                    }
                </style>
            </head>
            <body>
                <h1>You are now logged into your Azure DevOps account!</h1>
                <p>Thank you for logging in. You can now close this tab and return to your chat.</p>
            </body>
            </html>";

            return Content(htmlResponse, "text/html");
        }

        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error starting Azure DevOps authentication");
            return StatusCode(500, "Unable to generate an Azure DevOps token for your repository - please check if you have all the necessary permissions.");
        }
    }

    [HttpPost("aadauth/complete")]
#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation
    public async Task<IActionResult> CompleteAzureDevOpsAuth([FromQuery] string organization)
#pragma warning restore CUSTOM004
    {
        try
        {
            // Read access token from Authorization header
            var authHeader = Request.Headers["Authorization"].FirstOrDefault();
            if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogInternalWarning("CompleteAzureDevOpsAuth called without valid Authorization header");
                return BadRequest("Authorization header with Bearer token is required.");
            }

            var accessToken = authHeader.Substring("Bearer ".Length).Trim();

            // Validate the JWT token - validates issuer (any Azure AD tenant),
            // audience (must be Azure DevOps), signature, and lifetime
            var validatedToken = _jwtValidationHelper.ValidateAzureDevOpsToken(accessToken);

            if (validatedToken == null)
            {
                _logger.LogInternalWarning("Token validation failed for organization {Organization}", organization);
                return Unauthorized(new { message = "Invalid or expired token", success = false });
            }

            // Log authentication details (without sensitive token data)
            _logger.LogExternalInformation(
                "Azure DevOps authentication completed for organization " + organization);

            // Read refresh token from custom header
            var refreshToken = Request.Headers["x-sreagent-exchanged-refresh-tokens"].FirstOrDefault();

            // Save tokens to database with proper expiration from the validated token
            await _threadRepository.CreateOrUpdateAzureDevOpsOAuthTokenAsync(
                new AzureDevOpsAccessToken(accessToken, ExpiresOn: validatedToken.ValidTo, refreshToken),
                organization);

            return Ok(new { message = "You are now logged into your Azure DevOps account!", success = true });
        }
        catch (Exception ex)
        {
            _logger.LogExternalWarning("Error completing Azure DevOps OAuth for organization " + organization);
            return StatusCode(500, new { message = "Failed to complete authentication", success = false, error = ex.Message });
        }
    }

    [HttpPost("link")]
    [AuthorizeArmOperation(ArmOperations.AgentGraphWriteActionId)]
    public async Task<IActionResult> LinkSourceCode([FromBody] LinkSourceCodeRequest request)
    {
        try
        {
            var appNodeId = request.ResourceId.ToLower().Replace("/", "_");
            string vertexFilter = $"hasId('{appNodeId}')";
            string query = $@"g.V().{vertexFilter}.has('isDeleted', false)";

            // if app has a namespace and subType starts with "k8s", this is a k8s resource
            if (!string.IsNullOrEmpty(request.Namespace) && !string.IsNullOrEmpty(request.ResourceName) && !string.IsNullOrEmpty(request.SubType) && request.SubType.StartsWith("k8s", StringComparison.OrdinalIgnoreCase))
            {
                // for AKS resources, resourceId is the AKS cluster resource id, not the specific object resource id in graph
                query = $@"g.V().has('resourceName','{request.ResourceName}').has('namespace','{request.Namespace}').has('resourceType','{request.SubType}').has('clusterResourceId','{request.ResourceId}').has('isDeleted', false).values('id')";
                var appResult = await _graphDbClient.Query(query);
                var appidList = appResult.ToList();
                if (appidList.Count == 0)
                {
                    return NotFound($"the resource {request.ResourceId} {request.ResourceName} is not found.");
                }
                appNodeId = appidList[0].ToString();
            }
            else
            {
                var appNodeResults = await _graphDbClient.Query(query);
                if (!appNodeResults.Any())
                {
                    return NotFound($"the resource {request.ResourceId} {request.ResourceName} is not found.");
                }
            }

            var sourceCodeNode = new SourceCodeRepoNode(request.RepoUrl);
            var sourceCodeNodeResults = await _graphDbClient.Query($"g.V('{sourceCodeNode.GetNodeId()}').hasLabel('{sourceCodeNode.GetNodeLabel()}').has('isDeleted', false)");

            if (!sourceCodeNodeResults.Any())
            {
                await _graphDbClient.AddOrUpdateNodeAsync(sourceCodeNode);
            }

            var edge = new NonCrawledEdge(appNodeId, sourceCodeNode.GetNodeId(), Constants.Relationships.ServesCode);
            await _graphDbClient.AddOrUpdateEdgeAsync(edge);

            _crawlerTriggerService.TriggerSourceCodeRepoCrawl(request.RepoUrl);

            return Ok("Source code linked successfully.");
        }

        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error linking source code");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("unlink")]
    [AuthorizeArmOperation(ArmOperations.AgentGraphWriteActionId)]
    public async Task<IActionResult> UnlinkSourceCode([FromBody] LinkSourceCodeRequest request)
    {
        try
        {
            string disconnectRepository = await _azureDevOpsWorkItemPlugin.DisconnectRepository(request.ResourceId);
            return Ok("Source code unlinked successfully.");
        }

        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error unlinking source code");
            return StatusCode(500, "Internal server error");
        }
    }

    public class LinkSourceCodeRequest
    {
        public required string ResourceId { get; set; }

        // This regex pattern should be same as the one used in the GitHubHelper.ParseGitHubUrl
        [RegularExpression(GraphService.AzDoRepoRegexPattern,
            ErrorMessage = "Repository URL must be a valid Azure DevOps HTTPS Git URL.")]
        public required string RepoUrl { get; set; }
        public string? Namespace { get; set; } // Optional, can be null
        public string? ResourceName { get; set; } // Optional, can be null
        public string? SubType { get; set; } // Optional, can be null
    }
}
