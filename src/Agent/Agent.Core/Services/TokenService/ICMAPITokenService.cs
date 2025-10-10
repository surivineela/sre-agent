using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services.TokenService;

public class ICMAPITokenService : ManagedIdentityTokenServiceBase
{
    private static readonly Lazy<ICMAPITokenService> instance = new Lazy<ICMAPITokenService>(() => new ICMAPITokenService());

    public static ICMAPITokenService Instance => instance.Value;
    protected override bool ManagedIdentityEnabled { get; set; }
    protected override string Resource { get; set; } = string.Empty;
    protected override string ClientId { get; set; } = string.Empty;
    protected override string? ResourceId { get; set; }
    protected override string TokenServiceName { get; set; } = string.Empty;
    protected override IAuthenticationService authenticationService { get; set; } = null!; // will set in Initialize
    protected override TokenCredential? TokenCredential { get; set; }
    protected override TokenRequestContext TokenRequestContext { get; set; }

    public void Initialize(IAuthenticationService authService, ActionSettings actionSettings, IncidentManagementSettings incidentManagementSettings, ILogger<ICMAPITokenService> logger)
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
        _ = StartTokenRefresh(logger);
    }
}
