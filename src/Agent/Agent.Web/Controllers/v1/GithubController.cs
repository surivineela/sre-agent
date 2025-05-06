using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Web.Models;
using Microsoft.AspNetCore.Mvc;

namespace Agent.Web.Controllers.v1;

[ApiController]
[Route("api/v1/[controller]")]
public class GithubController : ControllerBase
{
    private readonly IThreadRepository _threadRepository;
    public GithubController(IThreadRepository threadRepository)
    {
        _threadRepository = threadRepository;
    }

    [HttpPost("auth/complete")]
    public async Task<IActionResult> CompleteGitHubAuth([FromForm]string accessToken)
    {
        await _threadRepository.CreateOrUpdateGitHubAccessTokenAsync(new GitHubAccessToken(accessToken, DateTime.UtcNow.AddMinutes(8)));
        return Ok();
    }
}
