using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Runtime;
using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

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
            builder.Services.AddSingleton<IGraphDatabaseClient, GremlinGraphDatabaseClient>();
            builder.Services.AddSingleton<ArmResourceCrawlerFactory>();
            builder.Services.AddSingleton<AzureResourceGraphClient>();
            builder.Services.AddSingleton<ResourceGraphCrawler>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
            builder.Services.AddSingleton<IArmClientFactory, ArmClientFactory>();
            builder.Services.AddSingleton<IKubernetesClientFactory, KubernetesClientFactory>();
            builder.Services.AddKeyedSingleton<IKubernetesService, CrawlerKubernetesService>("Crawler");

            builder.Services.AddSingleton<CrawlerCommand>();
            builder.Services.AddSingleton<GraphCommand>();

            var host = builder.Build();
            CommandLineApplication commandLineApplication = new(throwOnUnexpectedArg: true);
            commandLineApplication.HelpOption("-?|-h|--help");

            commandLineApplication.Command("Crawl",
                (command) =>
                {
                    var cmd = host.Services.GetRequiredService<CrawlerCommand>();
                    cmd.CrawlResourceId(command);
                });
            commandLineApplication.Command("CleanUp",
                (command) =>
                {
                    var cmd = host.Services.GetRequiredService<CrawlerCommand>();
                    cmd.CleanUp(command);
                });

            commandLineApplication.Command("ExportGraph",
                (command) =>
                {
                    var cmd = host.Services.GetRequiredService<GraphCommand>();
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
