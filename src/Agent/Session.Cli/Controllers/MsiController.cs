using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Mvc;
using Session.Cli.Attributes;
using Session.Cli.Models;
using Session.Cli.Services;

namespace Session.Cli.Controllers;

[Produces("application/json")]
[ApiController]
[Route("/msi/token")]
[LocalhostOnly]
public class MsiController : Controller
{
    private readonly ILogger<MsiController> _logger;
    private readonly ITokenService _tokenService;

    public MsiController(ILogger<MsiController> logger, ITokenService tokenService)
    {
        _logger = logger;
        _tokenService = tokenService;
    }

    [HttpGet]
    public async Task<IActionResult> GetTokenAsync([FromQuery] string resource)
    {
        var token = await _tokenService.GetTokenAsync(resource);
        if (token == null)
        {
            return Ok(new MsiResponse
            {
                AccessToken = string.Empty,
                ExpiresOn = "0",
                Resource = string.Empty,
                TokenType = string.Empty,
                ClientId = string.Empty
            });
        }

        var response = new MsiResponse
        {
            AccessToken = token.Raw,
            ExpiresOn = ((DateTimeOffset)token.JwtToken.ValidTo).ToUnixTimeSeconds().ToString(),
            Resource = resource,
            TokenType = "Bearer",
            ClientId = token.JwtToken.Claims.FirstOrDefault(c => c.Type == "oid")?.Value ?? string.Empty
        };

        return Ok(response);
    }
}
