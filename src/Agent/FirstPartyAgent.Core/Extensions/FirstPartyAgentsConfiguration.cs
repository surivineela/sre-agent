// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.Repositories;
using Agent.Plugins;
using Agent.Plugins.Interface;
using Agent.Plugins.Models;
using Agent.Runtime.Communication;
using Azure.Identity;
using FirstPartyAgent.Core.Clients;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Plugins;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Core.Services.TokenService;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using IKustoPluginClient = FirstPartyAgent.Core.Plugins.Interfaces.IKustoPluginClient;
using ICMWorkflowSettings = FirstPartyAgent.Core.Configuration.ICMWorkflowSettings;
using Agent.Core.Services;
// Add alias for FirstPartyAgent's AzureSearchPlugin
using FirstPartyAzureSearchPlugin = FirstPartyAgent.Core.Plugins.AzureSearchPlugin;
using Agent.Core.Helpers;
using WebAppPlugin = FirstPartyAgent.Core.Plugins.WebAppPlugin;

namespace FirstPartyAgent.Core.Extensions
{
    public static class FirstPartyAgentsConfigurationExtensions
    {
        public static void RegisterServiceDependencies(this IServiceCollection services, IHostEnvironment environment)
        {
            services.RegisterFirstPartyAppSettings();
            services.AddSingleton<ISessionMessageService, SessionMessageService>();
            services.AddSingleton<FirstPartyAgent.Core.Plugins.TimePlugin>();
            services.AddSingleton<FirstPartyAgent.Core.Services.IICMAPIClient, FirstPartyAgent.Core.Services.ICMAPIClient>();
            services.AddSingleton<ObserverClientService>();
            services.AddSingleton<IBaseIcmWorkflowClient>(sp =>
            {
                var icmWorkflowSettings = sp.GetRequiredService<ICMWorkflowSettings>();
                if (icmWorkflowSettings.Enabled)
                {
                    var logger = sp.GetRequiredService<ILogger<BaseIcmWorkflowClient>>();
                    return new BaseIcmWorkflowClient(environment, logger, icmWorkflowSettings);
                }
                return new NullableBaseIcmWorkflowClient();
            });
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
            services.AddSingleton<IHandoffToAgentClient, HandoffToAgentClient>();
            services.AddSingleton<ICMPlugin>();
            services.AddSingleton<FirstPartyAgent.Core.Plugins.GenevaActionsPlugin>();
            services.AddSingleton<HttpRequestPlugin>();

            services.AddSingleton<KustoClient>();
            services.AddSingleton<IKustoPluginClient, KustoPluginClient>();
            services.AddSingleton<KustoPlugin>();
            
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

            services.AddSingleton<TsgCrawlerClient>();

            services.AddSingleton<RedisGenevaActionsPlugin>();
            services.AddSingleton<ColdStartPlugin>();
            services.AddSingleton<ATLPlugin>();

            services.AddSingleton<FirstPartyAgent.Core.Services.IAzureSearchClient, FirstPartyAgent.Core.Services.AzureSearchClient>();
            
            // Register FirstPartyAgent's AzureSearchPlugin with its interface using the alias
            services.AddSingleton<FirstPartyAgent.Core.Plugins.IAzureSearchPlugin, FirstPartyAzureSearchPlugin>();
            
            // Register AzureSearchPluginDefinition that depends on FirstPartyAgent's implementation
            services.AddSingleton<FirstPartyAgent.Core.Plugins.Definitions.AzureSearchPluginDefinition>();

            var threadRepository = new InMemoryThreadRepository(new NullLogger<InMemoryThreadRepository>());
            var sinkService = new SinkService(threadRepository, new NullLogger<SinkService>());
            services.AddSingleton<IThreadRepository>(threadRepository);
            services.AddSingleton<SinkService>(sinkService);
            services.AddSingleton<IGraphDatabaseClient, NullableGraphDatabaseClient>();
            services.AddSingleton<GitHubClient>();
            services.AddSingleton<IGithubIssuePlugin, GitHubIssuePlugin>();
            services.AddSingleton<GitHubIssuePluginDefinition>();

            services.AddSingleton<EmergingIssuePlugin>();
            services.AddSingleton<EmergingIssueManagerPlugin>();

           
            services.AddHttpClient();
            services.AddDevOpsHelperHttpClient();

            services.AddSingleton<ICMChartPlugin>();
            services.AddSingleton<WebAppPlugin>();
            services.AddSingleton<AzureAlertingClient>();
            services.AddSingleton<AzureAlertingPlugin>();
            services.AddSingleton<ControlPlanePlugin>();
            services.AddSingleton<Agent.Plugins.Implementation.ApplensDetectorPlugin>();
            services.AddSingleton<IStorageService>(sp =>
            {
                try
                {
                    var storageAccountSettings = sp.GetRequiredService<StorageAccountSettings>();
                    if (string.IsNullOrWhiteSpace(storageAccountSettings.AccountUrl))
                    {
                        return new StorageServiceDisabled();
                    }
                    return new StorageService(storageAccountSettings);
                }
                catch (Exception)
                {
                    return new StorageServiceDisabled();
                }
            });
            services.AddSingleton<ICMAgentInstructionGenerationService>();

            services.AddSingleton<DiagnosticsHelper>(sp =>
            {
                var applensSettings = sp.GetRequiredService<ApplensSettings>();
                var logger = sp.GetRequiredService<ILogger<DiagnosticsHelper>>();
                return new DiagnosticsHelper(logger, applensSettings, environment);
            });
            services.AddSingleton<Agent.Plugins.Services.Interfaces.IApplensService>(sp =>
            {
                var applensSettings = sp.GetRequiredService<ApplensSettings>();
                if (applensSettings.Enabled)
                {
                    var logger = sp.GetRequiredService<ILogger<ApplensService>>();
                    var diagnosticsHelper = sp.GetRequiredService<DiagnosticsHelper>();
                    return new ApplensService(applensSettings, diagnosticsHelper, logger);
                }
                return new ApplensServiceDisabled();
            });

            var azureSettings = services.BuildServiceProvider().GetRequiredService<IOptions<AzureSettings>>();
            var cosmosDbSettings = azureSettings.Value.CosmosDB;

            if (!string.IsNullOrWhiteSpace(cosmosDbSettings.Docs.AccountName))
            {
                services.AddSingleton<ICosmosDBService, CosmosDBService>();
                services.AddSingleton<IIcmAgentConfigService, IcmAgentConfigService>();
            }
            else
            {
                services.AddSingleton<ICosmosDBService, CosmosDBServiceDisabled>();
                services.AddSingleton<IIcmAgentConfigService, IcmAgentConfigServiceDisabled>();
            }

            var emergingIssueSettings = azureSettings.Value.EmergingIssue;
            if (emergingIssueSettings.Enabled)
            {
                services.AddSingleton<IEmergingIssueConfigService, EmergingIssueConfigService>();
            }
            else
            {
                services.AddSingleton<IEmergingIssueConfigService, EmergingIssueConfigServiceDisabled>();
            }

            services.AddSingleton<DevOpsHelperService>();
            services.AddSingleton<TsgFetcherService>();
            services.AddSingleton<HandoffToAgentPlugin>();
            services.AddSingleton<OneBranchApprovalService>();
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
                else if (!string.IsNullOrWhiteSpace(openAISettings.ManagedIdentityClientId))
                {
                    kernelBuilder.AddAzureOpenAIChatCompletion(
                        deploymentName: openAISettings.LLMDeploymentName,
                        endpoint: openAISettings.Endpoint,
                        new ManagedIdentityCredential(openAISettings.ManagedIdentityClientId));
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
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.IcmAgent);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.TsgCrawler);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.HandoffToAgentConfig);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.Applens);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.OneBranchApprovalService);
            services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.AgentHelper);

            return services;
        }
    }
}

