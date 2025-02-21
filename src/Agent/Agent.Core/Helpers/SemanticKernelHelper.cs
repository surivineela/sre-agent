// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Octokit;
using System.Net.Http;

namespace Agent.Core.Helpers;

public static class SemanticKernelHelper

{
    public static void ConfigService(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<AppIdentityUpdatePlugin>();
        serviceCollection.AddScoped<ChartPlugin>();
        serviceCollection.AddSingleton<Models.GitHubClient>();
        serviceCollection.AddScoped<GithubIssuePlugin>();
        serviceCollection.AddScoped<MemoryAnalysisPlugin>();
        serviceCollection.AddScoped<SubscriptionPlugin>();
        serviceCollection.AddScoped<TlsPlugin>();

        //serviceCollection.AddScoped<CodeAnalyzerPlugin>();
        //serviceCollection.AddSingleton<CodeAnalyzerService>();
        serviceCollection.AddSingleton<TeamsConnector>();

        // Configure Semantic Kernel
        serviceCollection.AddScoped<Kernel>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();

            var azureSettings = config.GetSection("Azure").Get<AzureSettings>();

            if (azureSettings == null)
            {
                throw new NullReferenceException("Azure settings are required.");
            }

            var kernelBuilder = Kernel.CreateBuilder();

            kernelBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: azureSettings.OpenAI.DeploymentName,
                    endpoint: azureSettings.OpenAI.Endpoint,
                    apiKey: azureSettings.OpenAI.ApiKey);

            kernelBuilder.Services.AddLogging(builder =>
            {
                // Use configuration for logging levels
                builder.AddConfiguration(config.GetSection("Logging"));
                builder.AddConsole();
            });

            // Register skills
            kernelBuilder.Plugins.AddFromType<DiagnosePlugin>("DiagnosePlugin");
            kernelBuilder.Plugins.AddFromType<MetricsPlugin>("MetricsPlugin");
            kernelBuilder.Plugins.AddFromType<RemediationPlugin>("RemediationPlugin");
            kernelBuilder.Plugins.AddFromType<AppConfigurationChecksPlugin>("SqlConnectionPlugin");
            kernelBuilder.Plugins.AddFromType<TimePlugin>("TimePlugin");

            // kernelBuilder.Plugins.AddFromType<MonitorPlugin>("MonitorPlugin");

            var curlPlugin = sp.GetRequiredService<TlsPlugin>();
            kernelBuilder.Plugins.AddFromObject(curlPlugin, "CurlPlugin");

            var createGithubWorkItemPlugin = sp.GetRequiredService<GithubIssuePlugin>();
            kernelBuilder.Plugins.AddFromObject(createGithubWorkItemPlugin, "CreateGithubWorkItemPlugin");

            var subscriptionPlugin = sp.GetRequiredService<SubscriptionPlugin>();
            kernelBuilder.Plugins.AddFromObject(subscriptionPlugin, "SubscriptionPlugin");

            // var repoPlugin = sp.GetRequiredService<CodeAnalyzerPlugin>();
            // kernelBuilder.Plugins.AddFromObject(repoPlugin, "CodeAnalyzerPlugin");

            var appIdentityUpdatePlugin = sp.GetRequiredService<AppIdentityUpdatePlugin>();
            kernelBuilder.Plugins.AddFromObject(appIdentityUpdatePlugin, "AppIdentityUpdatePlugin");

            var chartPlugin = sp.GetRequiredService<ChartPlugin>();
            kernelBuilder.Plugins.AddFromObject(chartPlugin, "ChartPlugin");

            return kernelBuilder.Build();
        });
    }
}

