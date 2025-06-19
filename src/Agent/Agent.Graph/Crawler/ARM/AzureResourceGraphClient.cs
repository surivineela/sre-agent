// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Azure.ResourceManager;
using Azure.ResourceManager.ResourceGraph;
using Azure.ResourceManager.ResourceGraph.Models;
using Azure.ResourceManager.Resources;

namespace Agent.Graph.Crawler.ARM;

public class AzureResourceGraphClient
{
    private readonly ArmClient _client;
    private Lazy<TenantResource> _tenantResource;

    public AzureResourceGraphClient(IArmClientFactory armClientFactory, CrawlerSettings crawlerSettings)
    {
        _client = armClientFactory.GetCrawlerArmClient();
        if (crawlerSettings.TenantId == null) throw new ArgumentNullException("TenantId");
        _tenantResource = new Lazy<TenantResource>(() => InitTenantResource(crawlerSettings.TenantId));
    }

    public TenantResource InitTenantResource(string tenantId)
    {
        foreach (var pages in _client.GetTenants().GetAll().AsPages())
        {
            foreach (var tenant in pages.Values)
            {
                if (tenant.HasData && tenant.Data.TenantId.HasValue && tenant.Data.TenantId == new Guid(tenantId))
                {
                    return tenant;
                }
            }
        }

        throw new InvalidOperationException($"Failed to initialize TenantResource for tenant ID: {tenantId}");
    }

    public async Task<ResourceQueryResult> Query(IList<string> subscriptions, string query)
    {
        var request = new ResourceQueryContent(query);
        foreach (var sub in subscriptions)
        {
            request.Subscriptions.Add(sub);
        }

        var result = await _tenantResource.Value.GetResourcesAsync(request);

        return result;
    }
}

