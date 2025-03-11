using Agent.Data.DatabaseManagers.GraphDatabase;

namespace Agent.Graph.Crawler.ARM;

public interface IArmResourceCrawler
{
    public IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node);
}
