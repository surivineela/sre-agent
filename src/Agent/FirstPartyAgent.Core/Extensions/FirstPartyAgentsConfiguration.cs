// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.Repositories;
using Agent.Plugins;
using Agent.Plugins.Models;
using Agent.Runtime.Communication;
using Azure.Identity;
using FirstPartyAgent.Core.Clients;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Plugins;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Core.Services.TokenService;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Extensions
{
    public static class FirstPartyAgentsConfigurationExtensions
    {
        public static void RegisterServiceDependencies(this IServiceCollection services, IHostEnvironment environment)
        {
            services.RegisterFirstPartyAppSettings();
            services.AddSingleton<ISessionMessageService, SessionMessageService>();
            services.AddSingleton<FirstPartyAgent.Core.Plugins.TimePlugin>();
            services.AddSingleton<IICMAPIClient, ICMAPIClient>();
            services.AddSingleton<ObserverClientService>();
            services.AddSingleton<BaseIcmWorkflowClient>();
            services.AddSingleton<AlertHandlerService>();
            services.AddSingleton<IICMWorkflowClient>(sp =>
            {
                var icmWorkflowSettings = sp.GetRequiredService<ICMWorkflowSettings>();
                if (icmWorkflowSettings.Enabled)
                {
                    var logger = sp.GetRequiredService<ILogger<ICMWorkflowClient>>();
                    return new ICMWorkflowClient(environment, logger, icmWorkflowSettings);
                }
                return new NullableICMWorkflowClient();
            });
            services.AddSingleton<AlertHandlerClient>();
            services.AddSingleton<ICMPlugin>();
            services.AddSingleton<GenevaActionsPlugin>();
            services.AddSingleton<HttpRequestPlugin>();

            services.AddSingleton<KustoServiceClientFactory>();
            services.AddSingleton<IKustoPlugin, KustoPlugin>();
            services.AddSingleton<KustoPluginSimple>();
            services.AddSingleton<KustoClient>();

            services.AddSingleton<ITeamsClient, TeamsClient>();
            services.AddSingleton<TeamsPlugin>();
            services.AddSingleton<TeamsChartPlugin>();
            services.AddSingleton<IAlertProcessingService, AlertProcessingService>();

            services.AddSingleton<IAzureDevOpsClient>(sp =>
            {
                var azureDevOpsSettings = sp.GetRequiredService<AzureDevOpsSettings>();
                if (!azureDevOpsSettings.Enabled)
                {
                    return new NullableAzureDevOpsRestClient();
                }
                return new AzureDevOpsRestClient(environment, azureDevOpsSettings);
            });

            services.AddSingleton<AzureDevOpsPlugin>();

            services.AddSingleton<RedisGenevaActionsPlugin>();
            services.AddSingleton<ColdStartPlugin>();

            services.AddSingleton<FirstPartyAgent.Core.Services.IAzureSearchClient, FirstPartyAgent.Core.Services.AzureSearchClient>();
            services.AddSingleton<IAzureSearchPlugin, AzureSearchPlugin>();
            services.AddSingleton<AzureSearchPluginDefinition>();

            var threadRepository = new InmemoryThreadRepository(new NullLogger<InmemoryThreadRepository>());
            var sinkService = new SinkService(threadRepository, new NullLogger<SinkService>());
            services.AddSingleton<IThreadRepository>(threadRepository);
            services.AddSingleton<SinkService>(sinkService);
            services.AddSingleton<IGraphDatabaseClient, NullableGraphDatabaseClient>();
            services.AddSingleton<GitHubClient>();
            services.AddSingleton<IGithubIssuePlugin, GitHubIssuePlugin>();
            services.AddSingleton<GitHubIssuePluginDefinition>();

            services.AddHttpClient();
            services.AddDevOpsHelperHttpClient();

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
                services.AddSingleton<IIcmAgentConfigService, IcmAgentConfigService>();
            }
            else
            {
                services.AddSingleton<ICosmosDBService, CosmosDBServiceDisabled>();
                services.AddSingleton<IIcmAgentConfigService, IcmAgentConfigServiceDisabled>();
            }

            services.AddSingleton<DevOpsHelperService>();
            services.AddSingleton<TsgFetcherService>();
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
            services.AddSingleton(sp =>
            {
                var icmApiSettings = sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.ICMAPI;
                var logger = sp.GetRequiredService<ILogger<ICMAPITokenService>>();
                ICMAPITokenService.Instance.Initialize(icmApiSettings, logger);
                return icmApiSettings;
            });
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.ICMWorkflows);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.Kusto);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.Observer);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.Teams);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.Storage);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.DevOps);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.AzureDevOps);

            return services;
        }
    }
}

