using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Azure.Core;
using Azure.Identity;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Agent.Core.Services;

public class AuthenticationService : IAuthenticationService
{
    private readonly CrawlerSettings _crawlerSettings;
    private readonly FederationSettings _federationSettings;
    private readonly IHostEnvironment _hostEnvironment;

    public AuthenticationService(CrawlerSettings crawlerSettings,
        FederationSettings federationSettings,
        IHostEnvironment hostEnvironment)
    {
        _crawlerSettings = crawlerSettings;
        _federationSettings = federationSettings;
        _hostEnvironment = hostEnvironment;
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
            if(id == null)
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
}
