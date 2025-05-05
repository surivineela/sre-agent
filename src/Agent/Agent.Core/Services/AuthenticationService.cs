using System.IdentityModel.Tokens.Jwt;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Azure.Core;
using Agent.Logging;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly ILogger<AuthenticationService> _logger;
    private readonly CrawlerSettings _crawlerSettings;
    private readonly FederationSettings _federationSettings;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly DashboardSettings _dashboardSettings;
    private readonly Lazy<IThreadRepository> _threadRepository;

    public AuthenticationService(
        CrawlerSettings crawlerSettings,
        FederationSettings federationSettings,
        DashboardSettings dashboardSettings,
        IHostEnvironment hostEnvironment,
        IServiceProvider serviceProvider)
    {
        _crawlerSettings = crawlerSettings;
        _federationSettings = federationSettings;
        _hostEnvironment = hostEnvironment;
        _dashboardSettings = dashboardSettings;

        // To avoid cyclic dependency between cosmos client
        _threadRepository = new Lazy<IThreadRepository>(() => serviceProvider.GetRequiredService<IThreadRepository>());
    }

    public TokenCredential GetCrawlerCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetManagedIdentityCredential(_crawlerSettings.Identity);
    }

    public TokenCredential GetDocumentDbCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetWorkloadIdentityCredential(_federationSettings.ClientId, _federationSettings.TenantId, _federationSettings.AuthorityHost);
    }

    public TokenCredential GetDtsCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetWorkloadIdentityCredential(_federationSettings.ClientId, _federationSettings.TenantId, _federationSettings.AuthorityHost);
    }

    public TokenCredential GetArmReadOperationCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        // will change to OBO in the future
        return GetManagedIdentityCredential(_crawlerSettings.Identity);
    }

    public TokenCredential GetArmOperationCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        // will change to OBO in the future
        return GetManagedIdentityCredential(_crawlerSettings.Identity);
    }

    private ManagedIdentityCredential GetManagedIdentityCredential(string? identity)
    {
        if (identity == null) throw new ArgumentNullException();

        var mi = ManagedIdentityId.SystemAssigned;
        if (!Constants.SystemManagedIdentityName.Equals(identity, StringComparison.OrdinalIgnoreCase))
        {
            var id = new ResourceIdentifier(identity);
            if (id == null)
            {
                throw new ArgumentException($"Invalid resource identifier for user assigned managed identity: {identity}");
            }
            mi = ManagedIdentityId.FromUserAssignedResourceId(id);
        }

        var credOptions = new ManagedIdentityCredentialOptions(mi);

        return new ManagedIdentityCredential(credOptions);
    }

    private WorkloadIdentityCredential GetWorkloadIdentityCredential(string clientId, string tenantId, string authorityHost)
    {
        var credOptions = new WorkloadIdentityCredentialOptions()
        {
            ClientId = clientId,
            TenantId = tenantId,
            AuthorityHost = new Uri(authorityHost),
        };

        return new WorkloadIdentityCredential(credOptions);
    }

    private DefaultAzureCredential GetDefaultAzureCredential()
    {
        return new DefaultAzureCredential();
    }

    public TokenCredential GetAzureMonitorWorkspaceCredential()
    {
        return GetDashboardCredential();
    }

    private TokenCredential GetDashboardCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetManagedIdentityCredential(_dashboardSettings.Identity);
    }

    public async Task<string> GetGrafanaAccessToken()
    {
        if (!string.IsNullOrEmpty(_dashboardSettings.GrafanaApiKey))
        {
            return _dashboardSettings.GrafanaApiKey;
        }

        var cred = GetDashboardCredential();
        // https://learn.microsoft.com/en-us/azure/managed-grafana/how-to-api-calls?tabs=post#get-an-access-token
        var token = await cred.GetTokenAsync(new TokenRequestContext(["ce34e7e5-485f-4d76-964f-b3d2b16d1e4f/.default"]), CancellationToken.None);
        return token.Token;
    }

    public TokenCredential GetAzureOpenAICredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetWorkloadIdentityCredential(_federationSettings.ClientId, _federationSettings.TenantId, _federationSettings.AuthorityHost);
    }

    public TokenCredential GetAppInsightsCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }
        return GetWorkloadIdentityCredential(_federationSettings.ClientId, _federationSettings.TenantId, _federationSettings.AuthorityHost);
    }

    public TokenCredential GetLogAnalyticsCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetWorkloadIdentityCredential(_federationSettings.ClientId, _federationSettings.TenantId, _federationSettings.AuthorityHost);
    }

    public async Task<TokenCredential?> GetArmWriteOperationCredential(ApprovalContext approvalContext)
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        var approval = await _threadRepository.Value.GetApprovalAsync(approvalContext.ThreadId, approvalContext.ApprovalId);
        if (approval == null || string.IsNullOrEmpty(approval.OboToken))
        {
            return null;
        }

        return GetOboTokenCredential(approval.OboToken);
    }

    private TokenCredential GetOboTokenCredential(string token)
    {
        AccessToken accessToken;
        try
        {
            var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);
            accessToken = new AccessToken(token, jwt.ValidTo);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, "Failed to parse access token.");
            // blindly set expiration to 1 hour later
            accessToken = new AccessToken(token, DateTimeOffset.UtcNow.AddHours(1));
        }

        return DelegatedTokenCredential.Create((_, _) => accessToken);
    }
}
