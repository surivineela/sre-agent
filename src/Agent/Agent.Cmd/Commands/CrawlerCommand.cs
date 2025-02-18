using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph.Crawler.ARM;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Agent.Cmd
{
    public class CrawlerCommand
    {
        private readonly ILogger _logger;
        private readonly IServiceProvider _sp;

        public CrawlerCommand(ILogger logger, IServiceProvider sp)
        {
            _logger = logger;
            _sp = sp;
        }

        public void CrawlSubscription(CommandLineApplication command)
        {
            command.Description = "Test crawling subscription";
            command.HelpOption("-?|-h|--help");
            var sub = command.Argument("sub", "The subscription of environment");

            command.OnExecute(async () =>
            {
                var crawler = _sp.GetRequiredService<ResourceGraphCrawler>();
                await crawler.Crawl(
                    [
                    new SubscriptionNode(sub.Value)
                    ]
                    );

                return 0;
            });
        }
    }
}
