using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;

namespace Agent.Graph.Crawler.ARM
{
    public class KubernetesNamespaceCrawler : IResourceCrawler
    {
        private readonly IKubernetesClientFactory _k8sClientFactory;

        public KubernetesNamespaceCrawler(IKubernetesClientFactory k8sClientFactory)
        {
            _k8sClientFactory = k8sClientFactory;
        }

        public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
        {
            yield break;
        }
    }
}
