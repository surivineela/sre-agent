using System.ComponentModel.DataAnnotations;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DatabaseClients.GraphDbClient.Nodes;
using Agent.Graph.Crawler.ARM;
using Agent.Plugins.Interface;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class AzureDevOpsController(
    ILogger<AzureDevOpsController> _logger,
    IThreadRepository _threadRepository,
    IAzureDevOpsWorkItemPlugin _azureDevOpsWorkItemPlugin,
    IGraphDatabaseClient _graphDbClient) : ControllerBase
{
    [HttpGet("auth/start")]
    public async Task<IActionResult> StartAzureDevOpsAuth([FromQuery]string resourceId)
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

    [HttpPost("link")]
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
            return Ok("Source code linked successfully.");
        }

        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Error linking source code");
            return StatusCode(500, "Internal server error");
        }
    }

    [HttpPost("unlink")]
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
        [RegularExpression(@"^https:\/\/(?:dev\.azure\.com\/|[\w-]+\.visualstudio\.com\/)[\w-]+\/[\w-]+\/_git\/[\w.-]+$", ErrorMessage = "Repository URL must be a valid Azure DevOps HTTPS Git URL.")]
        public required string RepoUrl { get; set; }
        public string? Namespace { get; set; } // Optional, can be null
        public string? ResourceName { get; set; } // Optional, can be null
        public string? SubType { get; set; } // Optional, can be null
    }
}
