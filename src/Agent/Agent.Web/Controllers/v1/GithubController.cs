using System.ComponentModel.DataAnnotations;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Logging;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class GithubController(
    ILogger<GithubController> _logger,
    IThreadRepository _threadRepository,
    IGraphDatabaseClient _graphDbClient) : ControllerBase
{
    [HttpPost("auth/complete")]
    public async Task<IActionResult> CompleteGitHubAuth([FromForm]string accessToken)
    {
        await _threadRepository.CreateOrUpdateGitHubAccessTokenAsync(new GitHubAccessToken(accessToken, ExpiresOn: null));
        return Ok();
    }

    [HttpPost("link")]
    public async Task<IActionResult> LinkSourceCode([FromBody] LinkSourceCodeRequest request)
    {
        try
        {
            var containerAppNodeId = request.ResourceId.ToLower().Replace("/", "_");
            string vertexFilter = $"hasId('{containerAppNodeId}')";
            string query = $@"g.V().{vertexFilter}";
            var containerAppNodeResults = await _graphDbClient.Query(query);
            if (!containerAppNodeResults.Any())
            {
                return NotFound($"the resource {request.ResourceId} is not found.");
            }

            string displayName = request.RepoUrl.Split('/').Last();
            string sourceCodeNodeId = request.RepoUrl.ToLower().Replace("/", "_").Replace(":", "_");
            string checkSourceCodeNodeQuery = $"g.V('{sourceCodeNodeId}').hasLabel('microsoft.source/repository')";
            var sourceCodeNodeResults = await _graphDbClient.Query(checkSourceCodeNodeQuery);

            if (!sourceCodeNodeResults.Any())
            {
                var properties = new Dictionary<string, object>
                    {
                        { "resourceId", request.RepoUrl },
                        { "subscriptionId", "githubrepo-sub" },
                        { "resourceGroupName", "githubrepo-rg" },
                        { "resourceName", displayName },
                        { "updateTs", DateTime.UtcNow.Ticks }
                    };

                await _graphDbClient.AddOrUpdateNodeAsync("microsoft.source/repository", sourceCodeNodeId, "microsoft.source/repository", properties);
            }

            await _graphDbClient.AddOrUpdateEdgeAsync(containerAppNodeId, sourceCodeNodeId, Constants.Relationships.ServesCode);
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
        [RegularExpression(@"https://github\.com[/:](?<owner>[\w.-]+)/(?<repo>[\w.-]+)\.(?:git)?", ErrorMessage = "Repository URL must be of the form https://github.com/owner/repo-name.git")]
        public required string RepoUrl { get; set; }
    }
}
