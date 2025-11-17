using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Agent.Core.Models.Session;
using Microsoft.AspNetCore.Mvc;
using Session.Proxy.Services;

[Produces("application/json")]
[ApiController]
[Route("/shellexecute")]
public partial class CliExecutionController : Controller
{
    private readonly IShellService _shellService;

    [GeneratedRegex("export\\s+AZURE_CLI_ACCESS_TOKEN=(\\S+)\\s+&&\\s+(.+)")]
    private static partial Regex LegacyCommandRegex();

    public CliExecutionController(IShellService shellService)
    {
        _shellService = shellService;
    }

    // backward compatibility
    [HttpPost]
    public async Task<IActionResult> ExecuteCommandLegacy([FromBody] SessionRequest request, [FromQuery] string identifier, CancellationToken cancellationToken)
    {
        if (request.Commands == null || request.Commands.Count < 3)
        {
            return BadRequest("Invalid command.");
        }

        var cmd = request.Commands[2];
        // command in format: $"export AZURE_CLI_ACCESS_TOKEN={accessToken} && {command}"
        var match = LegacyCommandRegex().Match(cmd);
        if (!match.Success || match.Groups.Count != 3)
        {
            return BadRequest("Invalid command format.");
        }

        var token = match.Groups[1].Value;
        var command = match.Groups[2].Value;
        var azCliRequest = new AzCliExecutionRequest
        {
            ShellScripts = command,
            TimeoutInSeconds = request.TimeoutInSeconds,
            AccessTokens = new Dictionary<string, string>
            {
                { "https://management.core.windows.net/.default", token },
            }
        };

        var result = await _shellService.ExecuteAzCli(azCliRequest, identifier, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Route("azcli")]
    public async Task<IActionResult> ExecuteAzCli([FromBody] AzCliExecutionRequest request, [FromQuery] string identifier, CancellationToken cancellationToken)
    {
        var result = await _shellService.ExecuteAzCli(request, identifier, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Route("kubectl")]
    public async Task<IActionResult> ExecuteKubectl([FromBody] KubectlExecutionRequest request, [FromQuery] string identifier, CancellationToken cancellationToken)
    {
        var result = await _shellService.ExecuteKubectl(request, identifier, cancellationToken);
        return Ok(result);
    }
}
