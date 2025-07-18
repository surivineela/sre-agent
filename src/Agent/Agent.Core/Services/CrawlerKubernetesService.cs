// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using k8s;

namespace Agent.Core.Services;
public class CrawlerKubernetesService : KubernetesService
{
    private readonly IKubernetesClientFactory _kubernetesClientFactory;

    public CrawlerKubernetesService(IKubernetesClientFactory kubernetesClientFactory)
    {
        _kubernetesClientFactory = kubernetesClientFactory;
    }

    public override async Task<IKubernetes> GetKubernetesClient(string resourceId)
    {
        var client = await _kubernetesClientFactory.CreateKubernetesClientFromResourceIdForCrawlerAsync(resourceId);
        if (client == null)
        {
            throw new InvalidOperationException($"Unable to get Kubernetes client for resource {resourceId}.");
        }

        return client;
    }
}

