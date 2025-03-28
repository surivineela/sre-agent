using Agent.Graph.Crawler.ARM;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Agent.Cmd
{
    public class CrawlerCommand
    {
        private readonly ILogger<CrawlerCommand> _logger;
        private readonly ResourceGraphCrawler _crawler;

        public CrawlerCommand(ILogger<CrawlerCommand> logger, ResourceGraphCrawler crawler)
        {
            _logger = logger;
            _crawler = crawler;
        }

        public void CrawlResourceId(CommandLineApplication command)
        {
            command.Description = "Crawl a resource id";
            command.HelpOption("-?|-h|--help");
            var resourceId = command.Argument("resourceId", "Resource Id");

            command.OnExecute(async () =>
            {
                await _crawler.Crawl([resourceId.Value]);
                return 0;
            });
        }

        public void CleanUp(CommandLineApplication command)
        {
            command.Description = "Clean up stale nodes";
            command.HelpOption("-?|-h|--help");
            var subId = command.Argument("subscriptionId", "Subscription Id");
            command.OnExecute(async () =>
            {
                await _crawler.CleanUp(subId.Value);
                return 0;
            });
        }
    }
}
