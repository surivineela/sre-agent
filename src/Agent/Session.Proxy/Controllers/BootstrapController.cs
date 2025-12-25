using System.Text.Json;
using Agent.Common.ApiModels;
using Microsoft.AspNetCore.Mvc;
using Session.Identity.Attributes;
using Session.Proxy.Services;

namespace Session.Proxy.Controllers;

/// <summary>
/// Controller for bootstrapping the identity provider with certificate and tokens in a single request.
/// </summary>
[Produces("application/json")]
[ApiController]
[Route("/bootstrap")]
[SessionMode(SessionMode.Proxy)]
public class BootstrapController : Controller
{
    private readonly ILogger<BootstrapController> _logger;
    private readonly IdentityProviderClient _identityProviderClient;

    public BootstrapController(
        ILogger<BootstrapController> logger,
        IdentityProviderClient identityProviderClient)
    {
        _logger = logger;
        _identityProviderClient = identityProviderClient;
    }

    /// <summary>
    /// Bootstrap the identity provider with managed identity and/or tokens.
    /// </summary>
    [HttpPost]
    public async Task<IActionResult> Bootstrap([FromBody] BootstrapRequest request)
    {
        var results = new List<BootstrapResultItem>();
        var hasError = false;

        // Add managed identity if provided with PFX certificate
        if (request.ManagedIdentity?.PfxBytes != null && request.ManagedIdentity.PfxBytes.Length > 0)
        {
            if (string.IsNullOrEmpty(request.ManagedIdentity.TenantId) || string.IsNullOrEmpty(request.ManagedIdentity.ClientId))
            {
                return BadRequest(new { error = "TenantId and ClientId are required when providing a managed identity" });
            }

            var (success, response, error) = await _identityProviderClient.AddManagedIdentityAsync(request.ManagedIdentity);

            if (!success)
            {
                hasError = true;
                results.Add(new BootstrapResultItem { Type = "managedIdentity", Success = false, Error = error });
            }
            else
            {
                var details = JsonSerializer.Deserialize<ManagedIdentityResultDetails>(response!);
                results.Add(new BootstrapResultItem { Type = "managedIdentity", Success = true, Details = details });
            }
        }

        // Add tokens if provided
        if (request.Tokens != null && request.Tokens.Count > 0)
        {
            var (success, error) = await _identityProviderClient.AddTokensAsync(request.Tokens);

            if (!success)
            {
                hasError = true;
                results.Add(new BootstrapResultItem { Type = "tokens", Success = false, Error = error });
            }
            else
            {
                results.Add(new BootstrapResultItem { Type = "tokens", Success = true });
            }
        }

        if (hasError)
        {
            return StatusCode(207, new BootstrapResponse { Message = "Bootstrap completed with errors", Results = results });
        }

        return Ok(new BootstrapResponse { Message = "Bootstrap completed successfully", Results = results });
    }
}
