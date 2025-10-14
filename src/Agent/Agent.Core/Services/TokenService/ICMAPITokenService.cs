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
    private ICMAPISettings _icmAPISettings = null!;
    private List<DataConnectorInstanceSettings> _dataConnectorInstanceSettings = null!;

    public void Initialize(
        IAuthenticationService authService,
        ActionSettings actionSettings,
        IncidentManagementSettings incidentManagementSettings,
        ILogger<ICMAPITokenService> logger,
        string? agentSpaceProxyEndpoint,
        List<DataConnectorInstanceSettings> dataConnectorInstanceSettings)
    {
        ManagedIdentityEnabled = !string.IsNullOrEmpty(actionSettings.Identity);
        if (incidentManagementSettings.Type == IncidentManagementType.Icm && !string.IsNullOrWhiteSpace(incidentManagementSettings.ConnectionUrl))
        {
            // allow overriding Resource with ConnectionName for E2E testing with PPE ICM endpoint
            Resource = incidentManagementSettings.ConnectionName!;
        }
        else
        {
            Resource = incidentManagementSettings.ICMAPI.IcmMSIResource;
        }
        ResourceId = actionSettings.Identity;
        TokenServiceName = "ICMAPITokenService";
        authenticationService = authService;
        _icmAPISettings = incidentManagementSettings.ICMAPI;
        _dataConnectorInstanceSettings = dataConnectorInstanceSettings;

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
    protected override async ValueTask<AccessToken> AcquireToken(
        TokenRequestContext requestContext,
        CancellationToken cancellationToken,
        ILogger logger)
    {
        // Check if there's an AgentSpace ICM data connector configured, get first one since we only support one for now
        var agentSpaceIcmConnector = _dataConnectorInstanceSettings?
            .FirstOrDefault(dc => dc.Source == DataConnectorSource.AgentSpace &&
                                 dc.DataConnectorType.Equals("icm", StringComparison.OrdinalIgnoreCase));

        if (agentSpaceIcmConnector != null)
        {
            logger.LogInternalInformation($"[AcquireToken] Found AgentSpace ICM data connector: {agentSpaceIcmConnector.Name}");
            ResourceId = agentSpaceIcmConnector.Identity;
            Resource = _icmAPISettings.IcmMSIResource + "/.default";

            if (!string.IsNullOrWhiteSpace(_agentSpaceProxyEndpoint) &&
                !string.IsNullOrWhiteSpace(ResourceId) &&
                !string.IsNullOrWhiteSpace(Resource))
            {
                try
                {
                    var token = await authenticationService.GetTokenFromAgentSpaceProxy(Resource, ResourceId);
                    logger.LogInternalInformation("[AcquireToken] Successfully acquired token via AgentSpaceProxy.");
                    return token;
                }
                catch (Exception ex)
                {
                    logger.LogInternalError($"[AcquireToken] ERROR: Failed to get token from AgentSpaceProxy: {ex}");
                    throw new InvalidOperationException($"Failed to get token from Agent Space Proxy: {ex.Message}", ex);
                }
            }
        }
        

        var baseToken = await base.AcquireToken(requestContext, cancellationToken, logger);
        logger.LogInternalInformation("[AcquireToken] Successfully acquired token using base implementation.");
        return baseToken;
    }

}
