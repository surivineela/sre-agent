using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services.TokenService;

public class ICMAPITokenService : ManagedIdentityTokenServiceBase
{
    private static readonly Lazy<ICMAPITokenService> instance = new Lazy<ICMAPITokenService>(() => new ICMAPITokenService());

    public static ICMAPITokenService Instance => instance.Value;
    
    private string? _agentSpaceProxyEndpoint;    
    protected override bool ManagedIdentityEnabled { get; set; }
    protected override string Resource { get; set; } = string.Empty;
    protected override string ClientId { get; set; } = string.Empty;
    protected override string? ResourceId { get; set; }
    protected override string TokenServiceName { get; set; } = string.Empty;
    protected override IAuthenticationService authenticationService { get; set; } = null!; // will set in Initialize
    protected override TokenCredential? TokenCredential { get; set; }
    protected override TokenRequestContext TokenRequestContext { get; set; }

    public void Initialize(IAuthenticationService authService, ActionSettings actionSettings, ICMAPISettings icmApiSettings, ILogger<ICMAPITokenService> logger, string? agentSpaceProxyEndpoint)
    {
        ManagedIdentityEnabled = !string.IsNullOrEmpty(actionSettings.Identity);
        Resource = icmApiSettings.IcmMSIResource;
        ResourceId = actionSettings.Identity;
        TokenServiceName = "ICMAPITokenService";
        authenticationService = authService;
        
        // Store settings for use in GetAuthorizationTokenAsync
        _agentSpaceProxyEndpoint = agentSpaceProxyEndpoint;
        
        _ = StartTokenRefresh(logger);
    }

    /// <summary>
    /// Override the token acquisition to use Agent Space Proxy when endpoint is configured,
    /// otherwise fall back to the base class implementation.
    /// </summary>
    /// <param name="requestContext">The token request context</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A ValueTask that represents the asynchronous token acquisition operation</returns>
    protected override async ValueTask<AccessToken> AcquireToken(TokenRequestContext requestContext, System.Threading.CancellationToken cancellationToken)
    {
        // Check if Agent Space Proxy endpoint is configured
        if (!string.IsNullOrWhiteSpace(_agentSpaceProxyEndpoint) && ResourceId != null && Resource != null)
        {
            try
            {
                // Use Agent Space Proxy to get the token
                return await authenticationService.GetTokenFromAgentSpaceProxy(Resource, ResourceId);
            }
            catch (Exception ex)
            {
                // Log the error and fall back to base implementation
                throw new InvalidOperationException($"Failed to get token from Agent Space Proxy: {ex.Message}", ex);
            }
        }

        // Fall back to the base class implementation when Agent Space Proxy is not configured
        return await base.AcquireToken(requestContext, cancellationToken);
    }
}
