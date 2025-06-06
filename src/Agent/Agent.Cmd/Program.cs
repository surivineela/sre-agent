// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Interfaces;
using Agent.Graph.Services;
using Agent.Runtime;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
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
            HostApplicationBuilder builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
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
            builder.Services.AddSingleton<ICrawlerService, ResourceGraphCrawlerService>();
            builder.Services.AddSingleton<IActivityLogService, ActivityLogService>();
            builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
            builder.Services.AddSingleton<IArmClientFactory, ArmClientFactory>();
            builder.Services.AddSingleton<IKubernetesClientFactory, KubernetesClientFactory>();
            builder.Services.AddKeyedSingleton<IKubernetesService, CrawlerKubernetesService>("Crawler");
            builder.Services.AddSingleton<CrawlerCommand>();
            builder.Services.AddSingleton<GraphCommand>();
            builder.Services.AddSingleton<ScenarioCommand>();

            builder.Services.AddCrawlerHttpClient();
            builder.Services.AddHttpClient();

            string llmDeploymentName = builder.Configuration["AppSettings:Core:Azure:OpenAI:LLMDeploymentName"];
            builder.Services.ConfigureAzureOpenAIClient();
            builder.Services.AddKeyedChatClient("function-invocation-enabled", serviceProvider => serviceProvider.GetRequiredService<AzureOpenAIClient>().AsChatClient(llmDeploymentName), ServiceLifetime.Singleton)
                .UseFunctionInvocation();

            var host = builder.Build();
            CommandLineApplication commandLineApplication = new(throwOnUnexpectedArg: true);
            commandLineApplication.HelpOption("-?|-h|--help");

            commandLineApplication.Command("Crawl",
                (command) =>
                {
                    var cmd = host.Services.GetRequiredService<CrawlerCommand>();
                    cmd.CrawlResourceId(command);
                });

            commandLineApplication.Command("CrawlActivityLog",
                (command) =>
                {
                    var cmd = host.Services.GetRequiredService<CrawlerCommand>();
                    cmd.CrawlFromActivityLog(command);
                });

            commandLineApplication.Command("ExportGraph",
                (command) =>
                {
                    var cmd = host.Services.GetRequiredService<GraphCommand>();
                    cmd.ExportGraph(command);
                });

            commandLineApplication.Command("ExportGraphML",
                (command) =>
                {
                    var cmd = host.Services.GetRequiredService<GraphCommand>();
                    cmd.ExportGraphML(command);
                });

            commandLineApplication.Command("RunScenario",
                (command) =>
                {
                    var cmd = host.Services.GetRequiredService<ScenarioCommand>();
                    cmd.RunScenario(command);
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

