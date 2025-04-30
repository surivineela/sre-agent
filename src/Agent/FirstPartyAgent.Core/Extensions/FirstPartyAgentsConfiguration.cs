// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Plugins;
using Agent.Plugins.Models;
using Azure.Identity;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Plugins;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Identity.Client.Platforms.Features.DesktopOs.Kerberos;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Extensions
{
    public static class FirstPartyAgentsConfigurationExtensions
    {
        public static void RegisterServiceDependencies(this IServiceCollection services)
        {
            services.RegisterFirstPartyAppSettings();
            services.AddSingleton<ISessionMessageService, SessionMessageService>();
            services.AddSingleton<FirstPartyAgent.Core.Plugins.TimePlugin>();
            services.AddSingleton<IICMAPIClient, ICMAPIClient>();
            services.AddSingleton<ObserverClientService>();
            services.AddSingleton<BaseIcmWorkflowClient>();
            services.AddSingleton<AlertHandlerService>();
            services.AddSingleton<ICMWorkflowClient>();
            services.AddSingleton<ICMPlugin>();
            services.AddSingleton<GenevaActionsPlugin>();
            services.AddSingleton<HttpRequestPlugin>();

            services.AddSingleton<KustoServiceClientFactory>();
            services.AddSingleton<IKustoPlugin, KustoPlugin>();

            services.AddSingleton<ITeamsClient, TeamsClient>();
            services.AddSingleton<TeamsPlugin>();
            services.AddSingleton<IAlertProcessingService, AlertProcessingService>();

            services.AddSingleton<ObserverClientService>();
            services.AddSingleton<IICMAPIClient, ICMAPIClient>();
            services.AddSingleton<ICMWorkflowClient, ICMWorkflowClient>();
            services.AddSingleton<ICMPlugin>();
            services.AddSingleton<GenevaActionsPlugin>();
            services.AddSingleton<RedisGenevaActionsPlugin>();

            services.AddSingleton<KustoClientService>();
            services.AddSingleton<IKustoPlugin, KustoPlugin>();

            services.AddSingleton<ITeamsClient, TeamsClient>();
            services.AddSingleton<TeamsPlugin>();

            services.AddSingleton<FirstPartyAgent.Core.Services.IAzureSearchClient, FirstPartyAgent.Core.Services.AzureSearchClient>();
            services.AddSingleton<IAzureSearchPlugin, AzureSearchPlugin>();
            services.AddSingleton<AzureSearchPluginDefinition>();
            services.AddSingleton<GitHubClient>();
            services.AddSingleton<IGithubIssuePlugin, GitHubIssuePlugin>();
            services.AddSingleton<GitHubIssuePluginDefinition>();

            services.AddSingleton<ICMChartPlugin>();
            services.AddSingleton<WebAppPlugin>();
            services.AddSingleton<AzureAlertingClient>();
            services.AddSingleton<AzureAlertingPlugin>();
            services.AddSingleton<IStorageService>(sp =>
            {
                var storageAccountSettings = sp.GetRequiredService<StorageAccountSettings>();
                if (string.IsNullOrWhiteSpace(storageAccountSettings.AccountUrl)) {
                    return new StorageServiceDisabled();
                }
                return new StorageService(storageAccountSettings);
            });


            var config = services.BuildServiceProvider().GetRequiredService<IConfiguration>();
            if (!string.IsNullOrWhiteSpace(config["AppSettings:Core:External:CosmosDB:AccountUrl"]))
            {
                services.AddSingleton<ICosmosDBService, CosmosDBService>();
            }
            else
            {
                services.AddSingleton<ICosmosDBService, CosmosDBServiceDisabled>();
            }
        }

        public static IServiceCollection ConfigureSemanticKernel(this IServiceCollection services)
        {
            services.AddSingleton<IKernelService, KernelService>();

            // Add a plugin-less simple semantic kernel for stand-alone chat completion tasks
            services.AddSingleton<Kernel>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();
                var azureSettings = sp.GetRequiredService<IOptions<AzureSettings>>();
                var openAISettings = azureSettings.Value.OpenAI;

                var kernelBuilder = Kernel.CreateBuilder();
                var _federationSettings = azureSettings.Value.Federation;
                if (!string.IsNullOrWhiteSpace(_federationSettings?.ClientId))
                {
                    kernelBuilder.AddAzureOpenAIChatCompletion(
                        deploymentName: openAISettings.LLMDeploymentName,
                        endpoint: openAISettings.Endpoint,
                        new WorkloadIdentityCredential(new WorkloadIdentityCredentialOptions()
                        {
                            ClientId = _federationSettings.ClientId,
                            TenantId = _federationSettings.TenantId,
                            AuthorityHost = new Uri(_federationSettings.AuthorityHost),
                        }));
                }
                else if (!string.IsNullOrWhiteSpace(openAISettings.ApiKey))
                {
                    kernelBuilder.AddAzureOpenAIChatCompletion(
                        deploymentName: openAISettings.LLMDeploymentName,
                        endpoint: openAISettings.Endpoint,
                        apiKey: openAISettings.ApiKey);
                }
                else
                {
                    kernelBuilder.AddAzureOpenAIChatCompletion(
                        deploymentName: openAISettings.LLMDeploymentName,
                        endpoint: openAISettings.Endpoint,
                        new DefaultAzureCredential());
                }
                
                kernelBuilder.Services.AddLogging(builder =>
                {
                    // Use configuration for logging levels
                    builder.AddConfiguration(config.GetSection("Logging"));
                    builder.AddConsole();
                });

                return kernelBuilder.Build();
            });

            return services;
        }

        public static IServiceCollection RegisterFirstPartyAppSettings(this IServiceCollection services)
        {
            services.AddOptionsWithValidateOnStart<AzureSettings>()
                .BindConfiguration("AppSettings:Core:Azure")
                .ValidateDataAnnotations();

            services.AddOptionsWithValidateOnStart<FirstPartyAgentExternalSettings>()
                .BindConfiguration("AppSettings:Core:External")
                .ValidateDataAnnotations();

            services.AddSingleton(sp => sp.GetRequiredService<IOptions<AzureSettings>>().Value.OpenAI);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.AzureAlerting);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.AzureSearch);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.GitHub);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.ICMAPI);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.ICMWorkflows);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.Kusto);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.Observer);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.Teams);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.Storage);

            return services;
        }
    }
}

