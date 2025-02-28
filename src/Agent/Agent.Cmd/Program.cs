using System;
using System.Reflection;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph.Crawler.ARM;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Agent.Core.Configuration;
using Microsoft.Extensions.Hosting;
using Agent.Runtime;

namespace Agent.Cmd
{
    internal class Program
    {
        static void Main(string[] args)
        {
            HostApplicationBuilder builder = Host.CreateApplicationBuilder();
            builder.LoadAppSettings();
            builder.ValidateAndRegisterAppSettings<AppSettings>();

            // Register DI dependencies using builder.Services
            builder.Services.AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
            });
            builder.Services.AddSingleton<IGraphDatabaseManager, GremlinGraphDatabaseManager>();
            builder.Services.AddSingleton<ArmResourceCrawlerFactory>();
            builder.Services.AddSingleton<AzureResourceGraphClient>();
            builder.Services.AddScoped<ResourceGraphCrawler>();

            var host = builder.Build();
            ILogger logger = host.Services.GetService<ILogger<Program>>();
            CommandLineApplication commandLineApplication = new(throwOnUnexpectedArg: true);
            commandLineApplication.HelpOption("-?|-h|--help");

            commandLineApplication.Command("Crawl",
                (command) =>
                {
                    var cmd = new CrawlerCommand(logger, host.Services);
                    cmd.CrawlSubscription(command);
                });

            commandLineApplication.Command("ExportGraph",
                (command) =>
                {
                    var cmd = new GraphCommand(logger, host.Services);
                    cmd.ExportGraph(command);
                });

            commandLineApplication.OnExecute(() =>
            {
                commandLineApplication.ShowHelp();
                return 0;
            });

            commandLineApplication.Execute(args);
        }
    }
}
