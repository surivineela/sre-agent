using Agent.Core.Configuration;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services.TokenService;
public class ICMAPITokenService: ManagedIdentityTokenServiceBase
{
    private static readonly Lazy<ICMAPITokenService> instance = new Lazy<ICMAPITokenService>(() => new ICMAPITokenService());

    public static ICMAPITokenService Instance => instance.Value;
    protected override bool ManagedIdentityEnabled { get; set; }
    protected override string Resource { get; set; }
    protected override string ClientId { get; set; }
    protected override string ResourceId { get; set; }
    protected override string TokenServiceName { get; set; }
    protected override TokenCredential TokenCredential { get; set; }
    protected override TokenRequestContext TokenRequestContext { get; set; }

    public void Initialize(ActionSettings actionSettings, ICMAPISettings icmApiSettings, ILogger<ICMAPITokenService> logger)
    {
        ManagedIdentityEnabled = !string.IsNullOrEmpty(actionSettings.Identity);
        Resource = icmApiSettings.IcmMSIResource;
        ResourceId = actionSettings.Identity;
        TokenServiceName = "ICMAPITokenService";
        StartTokenRefresh(logger);
    }
}
