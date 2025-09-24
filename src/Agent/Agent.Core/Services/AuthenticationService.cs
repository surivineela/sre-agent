using System.IdentityModel.Tokens.Jwt;
using System.Security.Cryptography.X509Certificates;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Framework.Reasoning.Models;
using Agent.Logging;
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
    private readonly AzureSearchSettings _azureSearchSettings;
    private readonly Lazy<IThreadRepository> _threadRepository;

    public AuthenticationService(
        ILogger<AuthenticationService> logger,
        CrawlerSettings crawlerSettings,
        ActionSettings actionSettings,
        FederationSettings federationSettings,
        DashboardSettings dashboardSettings,
        GitHubSettings gitHubSettings,
        AzureSearchSettings azureSearchSettings,
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
        _azureSearchSettings = azureSearchSettings;

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

    public TokenCredential GetSessionPoolCredential()
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

    public TokenCredential GetAgentSpaceProxyCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetWorkloadIdentityCredential(_federationSettings.ClientId, _federationSettings.TenantId, _federationSettings.AuthorityHost);
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

    public TokenCredential Get1PAgentKeyVaultCredential(string managedIdentityId)
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        if (ResourceIdentifier.TryParse(managedIdentityId, out _))
        {
            // This is true when the MSI is being specified as a resource ID via ARM settings
            return GetManagedIdentityCredential(managedIdentityId);
        }
        else
        {
            return GetManagedIdentityCredentialForClientId(managedIdentityId);
        }
    }

    public TokenCredential GetIcmApiCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetManagedIdentityCredential(_actionSettings.Identity);
    }

    public TokenCredential GetDataConnectorCredential(ConnectorAuthSettings connectorAuthSettings)
    {
        switch (connectorAuthSettings.AuthenticationType)
        {
            case ConnectorAuthType.ManagedIdentity:
                return GetManagedIdentityCredential(Constants.SystemManagedIdentityName);
            case ConnectorAuthType.UAMI:
                {
                    if (!string.IsNullOrEmpty(connectorAuthSettings.ManagedIdentityClientId))
                    {
                        return GetManagedIdentityCredentialForClientId(connectorAuthSettings.ManagedIdentityClientId);
                    }
                    else if (!string.IsNullOrEmpty(connectorAuthSettings.ManagedIdentityResourceId))
                    {
                        return GetManagedIdentityCredential(connectorAuthSettings.ManagedIdentityResourceId);
                    }
                    else
                    {
                        throw new InvalidOperationException("Either ManagedIdentityClientId or ManagedIdentityResourceId must be provided for UAMI authentication.");
                    }
                }
            case ConnectorAuthType.App:
                {
                    if (string.IsNullOrEmpty(connectorAuthSettings.ApplicationClientId) ||
                        string.IsNullOrEmpty(connectorAuthSettings.ApplicationCertificate) ||
                        string.IsNullOrEmpty(connectorAuthSettings.Authority))
                    {
                        throw new InvalidOperationException("ApplicationClientId, ApplicationCertificate, and Authority must be provided for App authentication.");
                    }

                    var certificate = System.Security.Cryptography.X509Certificates.X509Certificate2
                        .CreateFromPem(connectorAuthSettings.ApplicationCertificate);

                    return new ClientCertificateCredential(
                        connectorAuthSettings.Authority,
                        connectorAuthSettings.ApplicationClientId,
                        certificate);
                }
            case ConnectorAuthType.User:
            default:
                return GetDefaultAzureCredential();
        }
    }

    public TokenCredential GetAzureSearchCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetManagedIdentityCredentialForClientId(_azureSearchSettings.UserAssignedMIClientId);
    }

    public TokenCredential GetEventHubTraceExportCredential(EventHubTraceExporterOptions options)
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        if (!string.IsNullOrEmpty(options.FirstPartyAppCertificatePath) &&
            !string.IsNullOrEmpty(options.FirstPartyAppClientId) &&
            !string.IsNullOrEmpty(options.FirstPartyAppTenantId))
        {
            var certPem = File.ReadAllText($"{options.FirstPartyAppCertificatePath}/tls.crt");
            var keyPem = File.ReadAllText($"{options.FirstPartyAppCertificatePath}/tls.key");

            var certificate = X509Certificate2.CreateFromPem(certPem, keyPem);

            return new ClientCertificateCredential(options.FirstPartyAppTenantId, options.FirstPartyAppClientId, certificate,
                new ClientCertificateCredentialOptions
                {
                    SendCertificateChain = true
                });
        }
        else
        {
            throw new ArgumentException("FirstPartyAppCertificatePath, FirstPartyAppClientId, and FirstPartyAppTenantId must be provided for EventHub trace export credential.");
        }
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

        var oboTokens = approvalDoc.OboToken.Split(","); // Do not remove empty entries, as YARP sets empty token when failed to exchange obo token
        var scopes = (approvalDoc.OboTokenScope ?? Constants.DefaultOboTokenScope).Split(",");
        if (oboTokens.Length != scopes.Length)
        {
            throw new InvalidOperationException("The number of OboTokens does not match the number of OboTokenScopes in the approval document");
        }

        var tokens = new Dictionary<string, string>();
        for (int i = 0; i < oboTokens.Length; i++)
        {
            if (!tokens.ContainsKey(scopes[i]))
            {
                tokens[scopes[i]] = oboTokens[i];
            }
        }

        _logger.LogInternalInformation($"[{approval.ThreadId}] Obo credential will be used. Approval id: {approval.ApprovalId}. Token scopes: {string.Join(",", tokens.Keys)}");

        return GetOboTokenCredential(tokens);
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

    private ManagedIdentityCredential GetManagedIdentityCredentialForClientId(string clientId)
    {
        if (clientId == null) throw new ArgumentNullException(nameof(clientId));

        var mi = ManagedIdentityId.FromUserAssignedClientId(clientId);
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
#pragma warning disable CUSTOM003 // This is only used in local development
        var options = new DefaultAzureCredentialOptions
        {
            ExcludeInteractiveBrowserCredential = false,
            ExcludeAzureCliCredential = false,
            ExcludeEnvironmentCredential = false,
            ExcludeManagedIdentityCredential = true,
            ExcludeSharedTokenCacheCredential = true,
            ExcludeVisualStudioCodeCredential = true,
            ExcludeVisualStudioCredential = true, // visual stuido credential cannot retireve non-ARM token
        };
        return new DefaultAzureCredential(options); // CodeQL [SM05137] This is not used in production code and only used in local development.
#pragma warning restore CUSTOM003
    }

    private TokenCredential GetOboTokenCredential(Dictionary<string, string> tokens)
    {
        var accessTokens = new Dictionary<string, AccessToken>();
        foreach (var kvp in tokens)
        {
            try
            {
                var jwt = new JwtSecurityTokenHandler().ReadJwtToken(kvp.Value);
                accessTokens[kvp.Key] = new AccessToken(kvp.Value, jwt.ValidTo);
            }
            catch (Exception ex)
            {
                _logger.LogInternalError(ex, $"Failed to parse token for resource: {kvp.Key}.");
            }
        }

        if (accessTokens.Count == 0)
        {
            throw new InvalidOperationException("No valid OBO token found.");
        }

        return DelegatedTokenCredential.Create((context, _) =>
        {
            AccessToken accessToken;
            if (accessTokens.TryGetValue(context.Scopes[0], out accessToken))
            {
                return accessToken;
            }
            else
            {
                // backward compatibility
                return accessTokens.Values.First();
            }
        });
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

        return "http://diag-runtimehost-euap.trafficmanager.net";
    }
    #endregion

    #region PostgreSQL specific methods
    public TokenCredential GetPostgresSqlCredential()
    {
        if (_hostEnvironment.IsDevelopment())
        {
            return GetDefaultAzureCredential();
        }

        return GetManagedIdentityCredential(GetActionIdentity());
    }
    #endregion
}

