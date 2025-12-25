using System.Collections.Concurrent;
using System.IdentityModel.Tokens.Jwt;
using Agent.Common.ApiModels;
using Microsoft.Identity.Client;
using Session.Identity.Models;

namespace Session.Identity.Services;

/// <summary>
/// Token service that acquires tokens from Azure AD using a certificate credential via MSAL.NET.
/// </summary>
public class ManagedIdentityTokenService : ITokenService
{
    private readonly ILogger<ManagedIdentityTokenService> _logger;
    private readonly IManagedIdentityService _managedIdentityService;
    private readonly ConcurrentDictionary<string, CachedToken> _tokenCache = new();

    /// <summary>
    /// Buffer time before token expiration to trigger a refresh (5 minutes).
    /// </summary>
    private static readonly TimeSpan TokenExpirationBuffer = TimeSpan.FromMinutes(5);

    public ManagedIdentityTokenService(
        ILogger<ManagedIdentityTokenService> logger,
        IManagedIdentityService managedIdentityService)
    {
        _logger = logger;
        _managedIdentityService = managedIdentityService;
    }

    // Referenced and simplified: https://msazure.visualstudio.com/One/_git/AAPT-Antares-ManagedIdentity?path=/src/ManagedIdentity/Microsoft.Azure.AppService.ManagedIdentity.Clients/MsalClient.cs&version=GBdev&line=22&lineEnd=22&lineStartColumn=52&lineEndColumn=77&lineStyle=plain&_a=contents
    public async Task<Token?> GetTokenAsync(string scope)
    {
        var config = _managedIdentityService.GetManagedIdentityConfiguration();
        if (config == null)
        {
            _logger.LogWarning("No managed identity configured. Cannot acquire token for scope: {Scope}", scope);
            return null;
        }

        var managedIdentity = config.ManagedIdentity;

        // Normalize scope - ensure it ends with /.default for app-only tokens
        var normalizedScope = NormalizeScope(scope);
        // Create cache key based on client ID and scope
        var cacheKey = $"{managedIdentity.ClientId}:{normalizedScope}";

        // Check if we have a valid cached token
        if (_tokenCache.TryGetValue(cacheKey, out var cachedToken) && !cachedToken.IsExpired)
        {
            _logger.LogDebug("Returning cached token for scope: {Scope}. Expires: {ExpiresOn}", scope, cachedToken.ExpiresOn);
            return cachedToken.Token;
        }

        try
        {
            var uri = new UriBuilder(managedIdentity.AuthenticationEndpoint)
            {
                Path = managedIdentity.TenantId
            };

            var authority = uri.Uri.ToString();
            _logger.LogInformation(
                "Acquiring token for scope: {Scope} using managed identity: {Type}, ClientId: {ClientId}, TenantId: {TenantId}, Authority: {Authority}",
                scope, managedIdentity.Type, managedIdentity.ClientId, managedIdentity.TenantId, authority);


            var clientApp = ConfidentialClientApplicationBuilder
                    .Create(managedIdentity.ClientId)
                    .WithCertificate(config.Certificate)
                    .WithClientName("MSI.Microsoft.SREAgent")
                    .WithAuthority(authority, validateAuthority: false)
                    .WithInstanceDiscoveryMetadata(GetRegionalDiscoveryMetadata(managedIdentity.AuthenticationEndpoint, managedIdentity.TenantId))
                    .Build();

            // Acquire token for client
            var result = await clientApp
                .AcquireTokenForClient(new[] { normalizedScope })
                .WithSendX5C(true) // this is required for managed identity
                .ExecuteAsync();

            // Parse the token
            var handler = new JwtSecurityTokenHandler();
            var jwtToken = handler.ReadJwtToken(result.AccessToken);

            _logger.LogInformation(
                "Successfully acquired token for scope: {Scope}. Expires: {ExpiresOn}",
                scope, result.ExpiresOn);

            var token = new Token
            {
                JwtToken = jwtToken,
                Raw = result.AccessToken
            };

            // Cache the token
            _tokenCache[cacheKey] = new CachedToken(token, result.ExpiresOn.UtcDateTime);

            return token;
        }
        catch (MsalException ex)
        {
            _logger.LogError(ex, "MSAL error acquiring token for scope: {Scope}. Error: {ErrorCode}", scope, ex.ErrorCode);
            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire token for scope: {Scope}", scope);
            return null;
        }
    }

    public Task AddTokensAsync(Dictionary<string, string> tokens)
    {
        // This service doesn't store tokens - it acquires them dynamically
        _logger.LogWarning("AddTokensAsync called on ManagedIdentityTokenService - this operation is not supported");
        return Task.CompletedTask;
    }

    private static string NormalizeScope(string scope)
    {
        // Azure AD v2 uses scopes instead of resources
        // If the scope already ends with /.default, return as-is
        if (scope.EndsWith("/.default", StringComparison.OrdinalIgnoreCase))
        {
            return scope;
        }

        // If it's a URL, append /.default
        if (Uri.TryCreate(scope, UriKind.Absolute, out _))
        {
            var normalizedScope = scope.TrimEnd('/');
            return $"{normalizedScope}/.default";
        }

        // Otherwise, return as-is (might be a well-known scope)
        return scope;
    }

    private string GetRegionalDiscoveryMetadata(string authenticationEndpoint, string tenantId)
    {
        Uri authority = new Uri(new Uri(authenticationEndpoint), tenantId);
        const string RegionalDiscoveryMetaDataTemplate = "{{\"api-version\":\"1.1\",\"metadata\":[{{\"preferred_network\":\"{0}\",\"preferred_cache\":\"{0}\",\"aliases\":[\"{0}\",]}}]}}";

        return string.Format(RegionalDiscoveryMetaDataTemplate, authority.Host);
    }

    private class CachedToken
    {
        public Token Token { get; }
        public DateTime ExpiresOn { get; }

        public CachedToken(Token token, DateTime expiresOn)
        {
            Token = token;
            ExpiresOn = expiresOn;
        }

        public bool IsExpired => DateTime.UtcNow >= ExpiresOn - TokenExpirationBuffer;
    }
}
