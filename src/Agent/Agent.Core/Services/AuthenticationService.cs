using System.IdentityModel.Tokens.Jwt;
using System.Threading.Tasks;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Core.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly ILogger<AuthenticationService> _logger;
    private readonly CrawlerSettings _crawlerSettings;
    private readonly ActionSettings _actionSettings;
    private readonly FederationSettings _federationSettings;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly DashboardSettings _dashboardSettings;
    private readonly GitHubSettings _gitHubSettings;
    private readonly Lazy<IThreadRepository> _threadRepository;

    public AuthenticationService(
        ILogger<AuthenticationService> logger,
        CrawlerSettings crawlerSettings,
        ActionSettings actionSettings,
        FederationSettings federationSettings,
        DashboardSettings dashboardSettings,
        GitHubSettings gitHubSettings,
        IHostEnvironment hostEnvironment,
        IServiceProvider serviceProvider)
    {
        _logger = logger;
        _crawlerSettings = crawlerSettings;
        _actionSettings = actionSettings;
        _federationSettings = federationSettings;
        _hostEnvironment = hostEnvironment;
        _dashboardSettings = dashboardSettings;
        _gitHubSettings = gitHubSettings;

        // To avoid cyclic dependency between cosmos client
        _threadRepository = new Lazy<IThreadRepository>(() => serviceProvider.GetRequiredService<IThreadRepository>());
    }

    #region Credential to access AME managed resources
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

    public TokenCredential GetAzureOpenAICredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetWorkloadIdentityCredential(_federationSettings.ClientId, _federationSettings.TenantId, _federationSettings.AuthorityHost);
    }

    public TokenCredential GetSearchEndpointCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetWorkloadIdentityCredential(_federationSettings.ClientId, _federationSettings.TenantId, _federationSettings.AuthorityHost);
    }

    public TokenCredential GetAgentMemoryBlobStorageCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetWorkloadIdentityCredential(_federationSettings.ClientId, _federationSettings.TenantId, _federationSettings.AuthorityHost);
    }

    public TokenCredential GetAgentMemoryAzureAISearchCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetWorkloadIdentityCredential(_federationSettings.ClientId, _federationSettings.TenantId, _federationSettings.AuthorityHost);
    }

    public TokenCredential GetSearchPluginCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetManagedIdentityCredential(GetActionIdentity());
    }

    public TokenCredential GetStorageCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetWorkloadIdentityCredential(_federationSettings.ClientId, _federationSettings.TenantId, _federationSettings.AuthorityHost);
    }

    public TokenCredential GetObserverCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetManagedIdentityCredential(GetActionIdentity());
    }

    public TokenCredential GetAgentHelperCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetManagedIdentityCredential(GetActionIdentity());
    }
    #endregion


    #region Credential to access customer resources
    public TokenCredential GetCrawlerCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        // do not use obo for crawler
        return GetManagedIdentityCredential(_crawlerSettings.Identity);
    }

    public async Task<TokenCredential> GetArmOperationCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return await ApprovalAwareCredentialHelper(() => GetManagedIdentityCredential(GetActionIdentity()));
    }

    public async Task<TokenCredential> GetKubernetesOperationCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return await ApprovalAwareCredentialHelper(() => GetManagedIdentityCredential(GetActionIdentity()));
    }

    public TokenCredential GetAzureMonitorWorkspaceCredential()
    {
        return GetDashboardCredential();
    }

    public TokenCredential GetAppInsightsCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetManagedIdentityCredential(GetActionIdentity());
    }

    public TokenCredential GetLogAnalyticsCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetManagedIdentityCredential(GetActionIdentity());
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

    private TokenCredential GetDashboardCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetManagedIdentityCredential(_dashboardSettings.Identity);
    }

    public TokenCredential GetAzureDevOpsCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetManagedIdentityCredential(_crawlerSettings.Identity);
    }

    public async Task<string> GetGitHubAccessToken()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return _gitHubSettings.PatTokenOverride;
        }

        var token = await _threadRepository.Value.GetGitHubAccessTokenAsync();
        if (token == null || string.IsNullOrEmpty(token.AccessToken))
        {
            throw new InvalidOperationException("GitHub access token is not available. Please authenticate first.");
        }

        return token.AccessToken;
    }

    #endregion

    // helper method that prefers to use obo if approval is available in the context
    private async Task<TokenCredential> ApprovalAwareCredentialHelper(Func<TokenCredential> nonOboCredFunc)
    {
        var approval = ToolStatic.AsyncLocalApprovalContext.Value;

        // no approval in context or the tool explicitly says not to use OBO token
        if (approval == null || !approval.UseOboToken)
        {
            return nonOboCredFunc();
        }

        if (approval.ApprovalId == null)
        {
            throw new InvalidOperationException("Approval is required but not present");
        }

        var approvalDoc = await _threadRepository.Value.GetApprovalAsync(approval.ThreadId, approval.ApprovalId.Value);
        if (approvalDoc == null)
        {
            throw new InvalidOperationException("Approval document not found");
        }
        if (string.IsNullOrEmpty(approvalDoc.OboToken))
        {
            throw new InvalidOperationException("OboToken not found in the approval document");
        }

        _logger.LogInternalInformation($"[{approval.ThreadId}] Obo credential will be used. Approval id: {approval.ApprovalId}");

        return GetOboTokenCredential(approvalDoc.OboToken);
    }

    private ManagedIdentityCredential GetManagedIdentityCredential(string? identity)
    {
        if (identity == null) throw new ArgumentNullException(nameof(identity));

        var mi = ManagedIdentityId.SystemAssigned;
        if (!Constants.SystemManagedIdentityName.Equals(identity, StringComparison.OrdinalIgnoreCase))
        {
            var id = new ResourceIdentifier(identity);
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

    public string? GetActionIdentity()
    {
        if (string.IsNullOrEmpty(_actionSettings.Identity))
        {
            // This should only for legacy agents
            return _crawlerSettings.Identity;
        }

        return _actionSettings.Identity;
    }

    #region Applens specific methods
    public TokenCredential GetApplensCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetWorkloadIdentityCredential(_federationSettings.ClientId, _federationSettings.TenantId, _federationSettings.AuthorityHost);
    }

    public string GetApplensScope()
    {
        // Microsoft tenant (72f988bf-86f1-41af-91ab-2d7cd011db47)
        if (_federationSettings.TenantId == "72f988bf-86f1-41af-91ab-2d7cd011db47")
        {
            return "b9a1efcd-32ee-4330-834c-c04eb00f4b33/.default";
        }
        
        // AME tenant (33e01921-4d64-4f8c-a055-5bdaffd5e33d)
        if (_federationSettings.TenantId == "33e01921-4d64-4f8c-a055-5bdaffd5e33d")
        {
            return "0d7b6142-46a3-426a-ad6d-eed97c2a48ee/.default";
        }

        // Future tenants can be added here as needed
        throw new NotSupportedException($"Applens scope not configured for tenant: {_federationSettings.TenantId}");
    }

    public string GetApplensRuntimeHostUrl()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return "http://localhost:1743";
        }

        return "https://diag-runtimehost-prod.trafficmanager.net";
    }
    #endregion
}
