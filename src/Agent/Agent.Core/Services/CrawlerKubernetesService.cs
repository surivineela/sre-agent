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

    public override Task<IKubernetes?> GetKubernetesClient(string resourceId)
    {
        var client = _kubernetesClientFactory.CreateKubernetesClientForCrawlerAsync(resourceId);
        if (client == null)
        {
            throw new InvalidOperationException($"Unable to get Kubernetes client for resource {resourceId}.");
        }

        return client;
    }
}
