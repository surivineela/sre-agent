using Agent.Data.DatabaseClients.GraphDbClient;

namespace Agent.Graph.Crawler.ARM;

public interface IArmResourceCrawler
{
    public IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node);
}
