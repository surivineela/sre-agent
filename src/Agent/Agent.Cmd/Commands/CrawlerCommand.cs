// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Graph.Interfaces;
using Agent.Graph.Services;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Logging;

namespace Agent.Cmd
{
    public class CrawlerCommand
    {
        private readonly ILogger<CrawlerCommand> _logger;
        private readonly ICrawlerService _crawler;

        public CrawlerCommand(ILogger<CrawlerCommand> logger, ICrawlerService crawler)
        {
            _logger = logger;
            _crawler = crawler;
        }

        public void CrawlResourceId(CommandLineApplication command)
        {
            command.Description = "Crawl a resource id";
            command.HelpOption("-?|-h|--help");
            var resourceId = command.Argument("resourceId", "Resource Id");
            var cascade = command.Option("-c|--cascade", "Crawl discovered resources too", CommandOptionType.NoValue);
            var filters = command.Option("-f|--filters", "Only crawl specific resource types", CommandOptionType.MultipleValue);

            command.OnExecute(async () =>
            {
                await _crawler.CrawlAsync([resourceId.Value], filters?.Values.Count == 0 ? null : filters?.Values, cascade.HasValue());
                return 0;
            });
        }

        public void CrawlFromActivityLog(CommandLineApplication command)
        {
            command.Description = "Crawl a resource id from activity log";
            command.HelpOption("-?|-h|--help");
            var resourceId = command.Argument("resourceId", "Resource Id");
            var startTime = command.Argument("startTime", "Start time");
            var endTime = command.Argument("endTime", "End time");
            command.OnExecute(async () =>
            {
                _crawler.StartActivityLogCrawler([resourceId.Value]);
                await Task.Delay(-1);
                return 0;
            });
        }
    }
}

