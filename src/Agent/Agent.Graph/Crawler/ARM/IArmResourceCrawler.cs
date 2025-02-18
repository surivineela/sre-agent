using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Data.DatabaseManagers.GraphDatabase;

namespace Agent.Graph.Crawler.ARM
{
    public interface IArmResourceCrawler
    {
        public IAsyncEnumerable<ArmResourceNode> Crawl(ArmResourceNode node);
    }
}
