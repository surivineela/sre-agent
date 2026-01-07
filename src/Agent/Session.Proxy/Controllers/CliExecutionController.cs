using System.Threading.Tasks;
using Agent.Common.ApiModels.Session;
using Microsoft.AspNetCore.Mvc;
using Session.Proxy.Services;

[Produces("application/json")]
[ApiController]
[Route("/shellexecute")]
public class CliExecutionController : Controller
{
    private readonly IShellService _shellService;

    public CliExecutionController(IShellService shellService)
    {
        _shellService = shellService;
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
