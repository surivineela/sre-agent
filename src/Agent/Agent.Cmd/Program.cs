using System;
using System.Reflection;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Graph.Crawler.ARM;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Agent.Core.Configuration;

namespace Agent.Cmd
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var serviceProvider = BuildServiceProvider();
            ILogger logger = serviceProvider.GetService<ILogger<Program>>();
            CommandLineApplication commandLineApplication = new(throwOnUnexpectedArg: true);
            commandLineApplication.HelpOption("-?|-h|--help");

            commandLineApplication.Command("Crawl",
                (command) =>
                {
                    var cmd = new CrawlerCommand(logger, serviceProvider);
                    cmd.CrawlSubscription(command);
                });

            commandLineApplication.OnExecute(() =>
            {
                commandLineApplication.ShowHelp();
                return 0;
            });

            commandLineApplication.Execute(args);
        }

        private static IServiceProvider BuildServiceProvider()
        {
            var configBuilder = new ConfigurationBuilder();
            configBuilder.SetBasePath(Path.GetDirectoryName(Assembly.GetEntryAssembly()!.Location))
            .AddJsonFile("appsettings.json")
            .AddJsonFile("appsettings.development.json");
            var config = configBuilder.Build();

            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.AddConsole();
            });

            services.AddSingleton((IConfiguration)config);
            services.AddSingleton<IGraphDatabaseManager, GremlinGraphDatabaseManager>();

            services.AddSingleton<ArmResourceCrawlerFactory>();
            services.AddSingleton<AzureResourceGraphClient>();
            services.AddScoped<ResourceGraphCrawler>();

            services.AddApplicationConfiguration(config);

            return services.BuildServiceProvider();
        }
    }
}
