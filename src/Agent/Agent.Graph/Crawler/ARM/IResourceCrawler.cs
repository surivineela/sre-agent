using Agent.Data.DatabaseClients.GraphDbClient;

namespace Agent.Graph.Crawler.ARM;


public interface IResourceCrawler
{
    public IAsyncEnumerable<GraphNode> Crawl(GraphNode node);
}
