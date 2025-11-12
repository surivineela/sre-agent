using System.ComponentModel.DataAnnotations;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Graph.Crawler.ARM;
using Agent.Logging;
using Agent.Plugins.Services;
using Agent.Web.Authorization;
using Microsoft.AspNetCore.Mvc;
using ArmOperations = Agent.Core.Constants.ArmOperations;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class GithubController(
    ILogger<GithubController> _logger,
    IThreadRepository _threadRepository,
    IGraphDatabaseClient _graphDbClient,
    ICrawlerTriggerService _crawlerTriggerService) : ControllerBase
{
    [HttpPost("auth/complete")]
#pragma warning disable CUSTOM004 // HTTP action must declare AuthorizeArmOperation
    public async Task<IActionResult> CompleteGitHubAuth([FromForm] string accessToken)
#pragma warning restore CUSTOM004
    {
        await _threadRepository.CreateOrUpdateGitHubAccessTokenAsync(new GitHubAccessToken(accessToken, ExpiresOn: null));

        // Return an HTML response with a thank-you message
        var htmlResponse = @"
        <!DOCTYPE html>
        <html lang='en'>
        <head>
            <meta charset='UTF-8'>
            <meta name='viewport' content='width=device-width, initial-scale=1.0'>
            <title>GitHub Authentication Complete</title>
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
            <h1>You are now logged into your GitHub account!</h1>
            <p>Thank you for logging in. You can now close this tab and return to your chat.</p>
        </body>
        </html>";

        return Content(htmlResponse, "text/html");
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

    public class LinkSourceCodeRequest
    {
        public required string ResourceId { get; set; }

        // This regex pattern should be same as the one used in the GitHubHelper.ParseGitHubUrl
        [RegularExpression(GraphService.GithubRepoRegexPattern, ErrorMessage = "Repository URL must be of the form https://github.com/owner/repo-name or https://github.enterprise.domain.com/owner/repo-name")]
        public required string RepoUrl { get; set; }

        public string? Namespace { get; set; } // Optional, can be null
        public string? ResourceName { get; set; } // Optional, can be null
        public string? SubType { get; set; } // Optional, can be null
    }
}
