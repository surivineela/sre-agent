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
    private readonly ITokenAcquisition _tokenAcquisition;

    public AuthController(ILogger<AuthController> logger, ITokenAcquisition tokenAcquisition)
    {
        _logger = logger;
        _tokenAcquisition = tokenAcquisition;
    }

    [HttpGet("login")]
    public IActionResult Login([FromQuery] string? returnUrl = null)
    {
        var redirectUrl = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
        var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
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
    [HttpGet("arm-token")]
    public async Task<IActionResult> GetArmToken()
    {
        try
        {
            var scopes = new[] { "https://management.azure.com/user_impersonation" };
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);

            return Ok(new
            {
                accessToken,
                tokenType = "Bearer",
                scope = "https://management.azure.com/user_impersonation"
            });
        }
        catch (MicrosoftIdentityWebChallengeUserException)
        {
            // User needs to consent - trigger consent flow
            _logger.LogWarning("User needs to consent to ARM scope. Triggering consent flow.");
            
            // Redirect to consent endpoint with the required scope
            var consentUrl = $"/api/auth/consent?scope=https://management.azure.com/user_impersonation&returnUrl={HttpContext.Request.Path}";
            return Redirect(consentUrl);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire ARM token");
            return StatusCode(500, new { error = "Failed to acquire token for Azure Resource Manager", details = ex.Message });
        }
    }

    [HttpGet("consent")]
    public IActionResult Consent([FromQuery] string? scope = null, [FromQuery] string? returnUrl = null)
    {
        var redirectUrl = string.IsNullOrEmpty(returnUrl) ? "/" : returnUrl;
        
        var properties = new AuthenticationProperties
        {
            RedirectUri = redirectUrl
        };
        
        // Force user to consent by using the "consent" prompt
        properties.Items["prompt"] = "consent";
        
        return Challenge(properties, OpenIdConnectDefaults.AuthenticationScheme);
    }

    [Authorize]
    [HttpGet("graph-token")]
    public async Task<IActionResult> GetGraphToken()
    {
        try
        {
            var scopes = new[] { "https://graph.microsoft.com/.default" };
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);

            return Ok(new
            {
                accessToken,
                tokenType = "Bearer",
                scope = "https://graph.microsoft.com/.default"
            });
        }
        catch (MicrosoftIdentityWebChallengeUserException ex)
        {
            _logger.LogWarning(ex, "User needs to consent to Graph scope");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire Graph token");
            return StatusCode(500, new { error = "Failed to acquire token for Microsoft Graph" });
        }
    }

    [Authorize]
    [HttpGet("sre-agent-token")]
    public async Task<IActionResult> GetSreAgentToken()
    {
        try
        {
            var scopes = new[] { "https://azuresre.dev/.default" };
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);

            return Ok(new
            {
                accessToken,
                tokenType = "Bearer",
                scope = "https://azuresre.dev/.default"
            });
        }
        catch (MicrosoftIdentityWebChallengeUserException ex)
        {
            _logger.LogWarning(ex, "User needs to consent to SRE Agent scope");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire SRE Agent token");
            return StatusCode(500, new { error = "Failed to acquire token for SRE Agent" });
        }
    }

    [Authorize]
    [HttpGet("app-insights-token")]
    public async Task<IActionResult> GetAppInsightsToken()
    {
        try
        {
            var scopes = new[] { "https://api.applicationinsights.io/.default" };
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(scopes);

            return Ok(new
            {
                accessToken,
                tokenType = "Bearer",
                scope = "https://api.applicationinsights.io/.default"
            });
        }
        catch (MicrosoftIdentityWebChallengeUserException ex)
        {
            _logger.LogWarning(ex, "User needs to consent to App Insights scope");
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire App Insights token");
            return StatusCode(500, new { error = "Failed to acquire token for Application Insights" });
        }
    }
}
