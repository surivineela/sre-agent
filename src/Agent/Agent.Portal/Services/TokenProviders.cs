using Microsoft.Identity.Web;

namespace Agent.Portal.Services;

/// <summary>
/// Base class for token providers that use Microsoft.Identity.Web for token acquisition.
/// </summary>
public abstract class TokenProviderBase : ITokenProvider
{
    private readonly ITokenAcquisition _tokenAcquisition;
    private readonly ILogger _logger;

    protected TokenProviderBase(ITokenAcquisition tokenAcquisition, ILogger logger)
    {
        _tokenAcquisition = tokenAcquisition;
        _logger = logger;
    }

    public abstract string Identifier { get; }
    public abstract string[] Scopes { get; }

    public async Task<TokenResponse> GetTokenAsync()
    {
        try
        {
            var accessToken = await _tokenAcquisition.GetAccessTokenForUserAsync(Scopes);

            return new TokenResponse
            {
                AccessToken = accessToken,
                TokenType = "Bearer",
                Scope = string.Join(" ", Scopes)
            };
        }
        catch (MicrosoftIdentityWebChallengeUserException ex)
        {
            _logger.LogWarning(ex, "User needs to consent to {Identifier} scope.", Identifier);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to acquire {Identifier} token", Identifier);
            throw;
        }
    }
}

/// <summary>
/// Token provider for Azure Resource Manager (ARM) API.
/// </summary>
public class ArmTokenProvider : TokenProviderBase
{
    public ArmTokenProvider(ITokenAcquisition tokenAcquisition, ILogger<ArmTokenProvider> logger)
        : base(tokenAcquisition, logger)
    {
    }

    public override string Identifier => "arm";
    public override string[] Scopes => new[] { "https://management.azure.com/user_impersonation" };
}

/// <summary>
/// Token provider for Microsoft Graph API.
/// </summary>
public class GraphTokenProvider : TokenProviderBase
{
    public GraphTokenProvider(ITokenAcquisition tokenAcquisition, ILogger<GraphTokenProvider> logger)
        : base(tokenAcquisition, logger)
    {
    }

    public override string Identifier => "graph";
    public override string[] Scopes => new[] { "https://graph.microsoft.com/.default" };
}

/// <summary>
/// Token provider for SRE Agent API.
/// </summary>
public class SreAgentTokenProvider : TokenProviderBase
{
    public SreAgentTokenProvider(ITokenAcquisition tokenAcquisition, ILogger<SreAgentTokenProvider> logger)
        : base(tokenAcquisition, logger)
    {
    }

    public override string Identifier => "sreAgent";
    public override string[] Scopes => new[] { "https://azuresre.dev/Threads.ReadWrite.All" };
}

/// <summary>
/// Token provider for Application Insights API.
/// </summary>
public class AppInsightsTokenProvider : TokenProviderBase
{
    public AppInsightsTokenProvider(ITokenAcquisition tokenAcquisition, ILogger<AppInsightsTokenProvider> logger)
        : base(tokenAcquisition, logger)
    {
    }

    public override string Identifier => "appInsights";
    public override string[] Scopes => new[] { "https://api.applicationinsights.io/Data.Read" };
}
