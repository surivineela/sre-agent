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
            var resourceId = command.Argument("resourceId", "Resource Id");

            command.OnExecute(async () =>
            {
                var crawler = _sp.GetRequiredService<ResourceGraphCrawler>();
                var node = ArmResourceCrawlerFactory.CreateResourceNodeFromResourceIdentifier(resourceId.Value);

                await crawler.Crawl(
                    [
                    node
                    ]
                    );

                return 0;
            });
        }
    }
}
