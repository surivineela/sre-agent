using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.Core;
using k8s;
using k8s.Models;

namespace Agent.Graph.Crawler.ARM;

// literally do nothing
public class KubernetesDummyCrawler : IResourceCrawler
{
    public async IAsyncEnumerable<GraphNode> Crawl(GraphNode node)
    {
        yield break;
    }
}
