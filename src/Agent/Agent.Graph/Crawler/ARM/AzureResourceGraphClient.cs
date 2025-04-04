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
    private TenantResource _tenantResource;

    public AzureResourceGraphClient(IArmClientFactory armClientFactory, CrawlerSettings crawlerSettings)
    {
        _client = armClientFactory.GetCrawlerArmClient();
        if (crawlerSettings.TenantId == null) throw new ArgumentNullException("TenantId");
        InitTenantResource(crawlerSettings.TenantId);
    }

    public void InitTenantResource(string tenantId)
    {
        foreach (var pages in _client.GetTenants().GetAll().AsPages())
        {
            foreach (var tenant in pages.Values)
            {
                if (tenant.HasData && tenant.Data.TenantId.HasValue && tenant.Data.TenantId == new Guid(tenantId))
                {
                    _tenantResource = tenant!;
                    return;
                }
            }
        }
    }

    public async Task<ResourceQueryResult> Query(IList<string> subscriptions, string query)
    {
        var request = new ResourceQueryContent(query);
        foreach (var sub in subscriptions)
        {
            request.Subscriptions.Add(sub);
        }

        var result = await _tenantResource.GetResourcesAsync(request);

        return result;
    }
}

