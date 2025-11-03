using Agent.Portal.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OpenIdConnect;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Identity.Web;

namespace Agent.Portal.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly ILogger<AuthController> _logger;
    private readonly ITokenProviderFactory _tokenProviderFactory;

    public AuthController(ILogger<AuthController> logger, ITokenProviderFactory tokenProviderFactory)
    {
        _logger = logger;
        _tokenProviderFactory = tokenProviderFactory;
    }

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null, [FromQuery] string? prompt = null, [FromQuery] string? tenantId = null)
    {
        var redirectUrl = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
        
        // Support forcing consent with prompt=consent query parameter
        if (!string.IsNullOrEmpty(prompt))
        {
            properties.Items["prompt"] = prompt;
        }
        
        // Support tenant switching - force account selection when switching tenants
        if (!string.IsNullOrEmpty(tenantId))
        {
            properties.Items["tenant_hint"] = tenantId;
            // Force account picker when switching tenants
            if (string.IsNullOrEmpty(prompt))
            {
                properties.Items["prompt"] = "select_account";
            }
        }
        
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [HttpGet("logout")]
    public IActionResult Logout()
    {
        return SignOut(
            new AuthenticationProperties { RedirectUri = "/" },
            CookieAuthenticationDefaults.AuthenticationScheme,
            OpenIdConnectDefaults.AuthenticationScheme
        );
    }

    [HttpGet("switch-tenant")]
    public async Task<IActionResult> SwitchTenant([FromQuery] string tenantId, [FromQuery] string? returnUrl = null)
    {
        // Clear the local authentication session (but don't sign out from Azure AD)
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        
        // Redirect to login with the new tenant
        var redirectUrl = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
        var loginUrl = $"/api/auth/login?tenantId={Uri.EscapeDataString(tenantId)}&returnUrl={Uri.EscapeDataString(redirectUrl)}";
        
        return Redirect(loginUrl);
    }

    [HttpGet("user")]
    public IActionResult GetUser()
    {
        if (User.Identity?.IsAuthenticated != true)
        {
            return Ok(new { isAuthenticated = false });
        }

        var userInfo = new
        {
            isAuthenticated = true,
            name = User.Identity.Name ?? string.Empty,
            username = User.FindFirst("preferred_username")?.Value ?? User.Identity.Name ?? string.Empty,
            email = User.FindFirst("email")?.Value ?? User.FindFirst("preferred_username")?.Value ?? string.Empty,
            tenantId = User.FindFirst("tid")?.Value ?? string.Empty,
            objectId = User.FindFirst("oid")?.Value ?? string.Empty,
            claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
        };

        return Ok(userInfo);
    }

    [Authorize]
    [HttpGet("profile")]
    public IActionResult GetProfile()
    {
        var profile = new
        {
            name = User.Identity?.Name,
            email = User.FindFirst("email")?.Value ?? User.FindFirst("preferred_username")?.Value,
            tenantId = User.FindFirst("tid")?.Value,
            objectId = User.FindFirst("oid")?.Value,
            claims = User.Claims.Select(c => new { c.Type, c.Value }).ToList()
        };

        return Ok(profile);
    }

    [Authorize]
    [HttpGet("get-token")]
    public async Task<IActionResult> GetToken([FromQuery] string type)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            return BadRequest(new { error = "Token type parameter is required" });
        }

        try
        {
            var provider = _tokenProviderFactory.GetProvider(type);
            var tokenResponse = await provider.GetTokenAsync();

            return Ok(new
            {
                accessToken = tokenResponse.AccessToken,
                tokenType = tokenResponse.TokenType,
                scope = tokenResponse.Scope
            });
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid token type requested: {Type}", type);
            return BadRequest(new { error = ex.Message });
        }
        catch (MicrosoftIdentityWebChallengeUserException ex)
        {
            _logger.LogWarning(ex, "User needs to consent to {Type} scope.", type);
            
            // Redirect to login with consent prompt
            var returnUrl = $"{HttpContext.Request.Scheme}://{HttpContext.Request.Host}{HttpContext.Request.Path}?type={Uri.EscapeDataString(type)}";
            return Redirect($"/api/auth/login?prompt=consent&returnUrl={Uri.EscapeDataString(returnUrl)}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire {Type} token", type);
            return StatusCode(500, new { error = $"Failed to acquire token for {type}", details = ex.Message });
        }
    }
}
