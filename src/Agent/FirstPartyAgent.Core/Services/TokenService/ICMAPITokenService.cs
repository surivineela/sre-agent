using Azure.Core;
using Microsoft.Extensions.Logging;
using FirstPartyAgent.Core.Configuration;
using Azure.Identity;

namespace FirstPartyAgent.Core.Services.TokenService;
public class ICMAPITokenService: ManagedIdentityTokenServiceBase
{
    private static readonly Lazy<ICMAPITokenService> instance = new Lazy<ICMAPITokenService>(() => new ICMAPITokenService());

    public static ICMAPITokenService Instance => instance.Value;
    protected override bool ManagedIdentityEnabled { get; set; }
    protected override string Resource { get; set; } = string.Empty;
    protected override string ClientId { get; set; } = string.Empty;
    protected override string TokenServiceName { get; set; } = string.Empty;
    protected override TokenCredential TokenCredential { get; set; } = new DefaultAzureCredential();
    protected override TokenRequestContext TokenRequestContext { get; set; }
    public void Initialize(ICMAPISettings icmApiSettings, ILogger<ICMAPITokenService> logger)
    {
        ManagedIdentityEnabled = icmApiSettings.ManagedIdentityEnabled;
        Resource = icmApiSettings.IcmMSIResource;
        ClientId = icmApiSettings.ManagedIdentityClientId;
        TokenServiceName = "ICMAPITokenService";
        _ = StartTokenRefresh(logger);
    }
}
