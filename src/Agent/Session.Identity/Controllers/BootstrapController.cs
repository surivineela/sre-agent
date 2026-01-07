using System.Security.Cryptography.X509Certificates;
using Agent.Common.ApiModels;
using Microsoft.AspNetCore.Mvc;
using Session.Identity.Attributes;
using Session.Identity.Models;
using Session.Identity.Services;

namespace Session.Identity.Controllers;

/// <summary>
/// Controller for bootstrap operations including managed identity and token management.
/// </summary>
[Produces("application/json")]
[ApiController]
[Route("/bootstrap")]
[LocalhostOnly]
public class BootstrapController : Controller
{
    private readonly ILogger<BootstrapController> _logger;
    private readonly ITokenService _tokenService;
    private readonly IManagedIdentityService _managedIdentityService;

    public BootstrapController(
        ILogger<BootstrapController> logger,
        ITokenService tokenService,
        IManagedIdentityService managedIdentityService)
    {
        _logger = logger;
        _tokenService = tokenService;
        _managedIdentityService = managedIdentityService;
    }

    [HttpPost("tokens")]
    public async Task<IActionResult> AddTokensAsync([FromBody] AddTokensRequest request)
    {
        if (request.Tokens == null || request.Tokens.Count == 0)
        {
            return BadRequest("No tokens provided");
        }

        await _tokenService.AddTokensAsync(request.Tokens);

        _logger.LogInformation("Added {Count} tokens", request.Tokens.Count);

        return Ok(new { message = $"Added {request.Tokens.Count} tokens" });
    }

    [HttpPost("managedIdentity")]
    public IActionResult AddManagedIdentity([FromBody] ManagedIdentityRequest request)
    {
        var managedIdentity = request.ManagedIdentity;

        if (managedIdentity == null)
        {
            return BadRequest("Managed identity information is required");
        }

        if (managedIdentity.PfxBytes == null || managedIdentity.PfxBytes.Length == 0)
        {
            return BadRequest("Certificate data (pfxBytes) is required");
        }

        if (string.IsNullOrEmpty(managedIdentity.TenantId))
        {
            return BadRequest("TenantId is required");
        }

        if (string.IsNullOrEmpty(managedIdentity.ClientId))
        {
            return BadRequest("ClientId is required");
        }

        if (string.IsNullOrEmpty(managedIdentity.Type))
        {
            return BadRequest("Type is required");
        }

        if (string.IsNullOrEmpty(managedIdentity.PrincipalId))
        {
            return BadRequest("PrincipalId is required");
        }

        if (string.IsNullOrEmpty(managedIdentity.AuthenticationEndpoint))
        {
            return BadRequest("AuthenticationEndpoint is required");
        }

        try
        {
            _managedIdentityService.StoreManagedIdentity(managedIdentity);

            _logger.LogInformation(
                "Managed identity configured successfully. Type: {Type}, ClientId: {ClientId}",
                managedIdentity.Type ?? "SystemAssigned",
                managedIdentity.ClientId);

            return Ok(new
            {
                message = "Managed identity configured successfully",
                type = managedIdentity.Type ?? "SystemAssigned",
                clientId = managedIdentity.ClientId,
                principalId = managedIdentity.PrincipalId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to process managed identity certificate");
            return BadRequest($"Failed to process managed identity: {ex.Message}");
        }
    }
}
