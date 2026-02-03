// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Agent.Core.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Protocols;
using Microsoft.IdentityModel.Protocols.OpenIdConnect;
using Microsoft.IdentityModel.Tokens;

namespace Agent.Core.Helpers;

public sealed class JwtValidationHelper
{
    private readonly ILogger<JwtValidationHelper> _logger;
    private readonly JwtSecurityTokenHandler _tokenHandler;

    // Azure DevOps valid audience values
    private static readonly string[] AzureDevOpsAudiences = new[]
    {
        "499b84ac-1321-427f-aa17-267ca6975798", // Azure DevOps Application ID
        "https://app.vssps.visualstudio.com",
        "api://AzureADTokenExchange"
    };

    public JwtValidationHelper(
        ILogger<JwtValidationHelper> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _tokenHandler = new JwtSecurityTokenHandler();
    }

    /// <summary>
    /// Validates a JWT token for Azure DevOps and returns the validated claims principal.
    /// Accepts tokens from any Azure AD tenant but requires Azure DevOps audience.
    /// </summary>
    /// <param name="token">The JWT token to validate</param>
    /// <returns>JwtSecurityToken if validation succeeds</returns>
    public JwtSecurityToken ValidateAzureDevOpsToken(string token)
    {
        JwtSecurityToken? unvalidatedToken = null;

        // First, read the token to extract tenant ID for OIDC discovery
        unvalidatedToken = _tokenHandler.ReadJwtToken(token);
        var tenantId = unvalidatedToken.Claims.FirstOrDefault(c => c.Type == "tid")?.Value;

        if (string.IsNullOrEmpty(tenantId))
        {
            throw new SecurityTokenValidationException("Token does not contain tenant ID (tid) claim");
        }

        // Get OIDC configuration for the token's tenant
        var authority = $"https://login.microsoftonline.com/{tenantId}/v2.0";
        var metadataAddress = $"{authority}/.well-known/openid-configuration";

        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            // Accept tokens from any Azure AD tenant
            ValidIssuers = new[]
            {
                $"https://login.microsoftonline.com/{tenantId}/v2.0",
                $"https://sts.windows.net/{tenantId}/",
                $"https://login.microsoftonline.com/{tenantId}/",
                $"https://sts.windows.net/{tenantId}"
            },
            ValidateAudience = true,
            // Must be Azure DevOps audience
            ValidAudiences = AzureDevOpsAudiences,
            ValidateIssuerSigningKey = false,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(5), // Allow 5 minutes clock skew
            RequireExpirationTime = true,
            RequireSignedTokens = true
        };

        var principal = _tokenHandler.ValidateToken(token, validationParameters, out var validatedToken);

        // Additional validation: ensure it's a JWT token
        if (validatedToken is not JwtSecurityToken jwtToken || validatedToken == null)
        {
            throw new SecurityTokenValidationException("Token is not a valid JWT token");
        }

        return jwtToken;
    }
}

public sealed record UserInfo
{
    public string? UserId { get; init; }
    public string? Email { get; init; }
    public string? Name { get; init; }
    public string? GivenName { get; init; }
    public string? FamilyName { get; init; }
    public string? TenantId { get; init; }
    public string? Issuer { get; init; }
    public DateTime ExpiresAt { get; init; }
}
