// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ClientModel;
using System.ClientModel.Primitives;
using Agent.Core;
using Agent.Core.Clients.Chat;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Framework;
using Agent.Logging;
using Agent.Runtime.Services;
using Azure.AI.OpenAI;
using Kusto.Cloud.Platform.Utils;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using OpenAI;

namespace Agent.Runtime
{
    public static class AgentsConfigurationExtensions
    {
        public static IServiceCollection ConfigureIChatCompletionService(this IServiceCollection services)
        {
            return services
                .AddSingleton<IChatCompletionService>(sp =>
                {
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();

                    // TODO: remove api key after CP is deployed
                    if (!string.IsNullOrEmpty(openAISettings.ApiKey))
                    {
                        return new AzureOpenAIChatCompletionService(
                            deploymentName: openAISettings.LLMDeploymentName,
                            endpoint: openAISettings.Endpoint,
                            apiKey: openAISettings.ApiKey
                        );
                    }
                    else
                    {
                        var authService = sp.GetRequiredService<IAuthenticationService>();
                        var cred = authService.GetAzureOpenAICredential();
                        return new AzureOpenAIChatCompletionService(
                            deploymentName: openAISettings.LLMDeploymentName,
                            endpoint: openAISettings.Endpoint,
                            cred
                        );
                    }
                });
        }

        public static IServiceCollection ConfigureAzureOpenAIClient(this IServiceCollection services)
        {
            return services
                .ConfigureAOAIClient()
                .ConfigureOAIClient();
        }

        private static IServiceCollection ConfigureAOAIClient(this IServiceCollection services)
        {
            return services
                .AddSingleton(sp =>
                {
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    // use a typed logger so logs are categorized under the policy type
                    var logger = loggerFactory.CreateLogger<AzureOpenAILoggingPolicy>();

                    // create options with a custom retry policy that logs 429 responses
                    var options = new AzureOpenAIClientOptions()
                    {
                        NetworkTimeout = TimeSpan.FromMinutes(5),
                        RetryPolicy = new ClientRetryPolicy(2)
                    };

                    // register a pipeline policy that logs 429 responses
                    options.AddPolicy(new AzureOpenAILoggingPolicy(logger), PipelinePosition.PerCall);

                    // TODO: remove api key after CP is deployed
                    if (!string.IsNullOrEmpty(openAISettings.ApiKey))
                    {
                        return new AzureOpenAIClient(
                            endpoint: new(openAISettings.Endpoint),
                            credential: new ApiKeyCredential(openAISettings.ApiKey),
                            options: options
                        );
                    }
                    else
                    {
                        var authService = sp.GetRequiredService<IAuthenticationService>();
                        var cred = authService.GetAzureOpenAICredential();
                        return new AzureOpenAIClient(
                            endpoint: new(openAISettings.Endpoint),
                            credential: cred,
                            options: options
                        );
                    }
                });
        }

        private static IServiceCollection ConfigureOAIClient(this IServiceCollection services)
        {
            return services
                .AddSingleton(sp =>
                {
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    // use a typed logger so logs are categorized under the policy type
                    var logger = loggerFactory.CreateLogger<AzureOpenAILoggingPolicy>();

                    // use OAI Client is using GHCP Endpoint
                    if (!string.IsNullOrEmpty(openAISettings.GhcpEndpoint))
                    {
                        // create options with a custom retry policy that logs 429 responses
                        var options = new OpenAIClientOptions()
                        {
                            Endpoint = new(openAISettings.GhcpEndpoint),
                            NetworkTimeout = TimeSpan.FromMinutes(5),
                            RetryPolicy = new ClientRetryPolicy(2)
                        };

                        // register a pipeline policy that logs 429 responses
                        options.AddPolicy(new AzureOpenAILoggingPolicy(logger), PipelinePosition.PerCall);

                        return new OpenAIClient(
                            credential: new ApiKeyCredential("doesnotmatter"),
                            options: options
                        );
                    }
                    // use AOAI Client otherwise
                    else
                    {
                        return sp.GetRequiredService<AzureOpenAIClient>();
                    }
                });
        }

        // Pipeline policy that logs 429 responses using the project's structured logging helper
        internal class AzureOpenAILoggingPolicy : PipelinePolicy
        {
            private readonly ILogger _logger;

            public AzureOpenAILoggingPolicy(ILogger logger)
            {
                _logger = logger;
            }

            public override void Process(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int index)
            {
                // Invoke next policy in pipeline
                ProcessNext(message, pipeline, index);

                try
                {
                    if (message.Response != null && message.Response.Status == 429)
                    {
                        _logger.LogAgentActionError(new Exception("Azure OpenAI returned 429 Too Many Requests"),
                            AgentActionEvents.GenerateModelResponse, string.Empty, "429", 0, string.Empty);
                    }
                }
                catch
                {
                    // Swallow logging errors so we don't affect the caller
                }
            }

            public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int index)
            {
                await ProcessNextAsync(message, pipeline, index).ConfigureAwait(false);

                try
                {

                    if (message.Response != null && message.Response.Status == 429)
                    {
                        _logger.LogAgentActionError(new Exception("Azure OpenAI returned 429 Too Many Requests"),
                            AgentActionEvents.GenerateModelResponse, string.Empty, "429", 0, string.Empty);
                    }
                }
                catch
                {
                    // ignore
                }
            }
        }

        public static IServiceCollection ConfigureIChatClient(this IServiceCollection services, IConfiguration configuration)
        {
            // backward compatibility
            // TODO: Will remove these settings once AvailableModels is fully rolled out
            var chatClientProviderSettings = configuration.GetSection("AppSettings:Core:ChatClientProvider").Get<ChatClientProviderSettings>();
            // register keyed IChatClient for all models in ChatClientProviderSettings.ModelNames
            if (chatClientProviderSettings != null && !string.IsNullOrWhiteSpace(chatClientProviderSettings.ModelNames))
            {
                var modelNames = chatClientProviderSettings.ModelNames
                    .Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(m => m.Trim())
                    .Where(m => !string.IsNullOrWhiteSpace(m))
                    .Distinct()
                    .ToList();

                foreach (var modelName in modelNames)
                {
                    services.AddKeyedSingleton(modelName, (sp, _) =>
                    {
                        return sp.CreateChatClientBuilder(openAiDeploymentName: modelName).Build();
                    });
                }
            }

            var agentModelSettings = configuration.GetSection("AppSettings:Core:AgentModel").Get<AgentModelSettings>();
            // register keyed IChatClient for all models in AgentModel.AvailableModels
            if (agentModelSettings != null)
            {
                var modelNames = agentModelSettings.AvailableModelList;

                foreach (var modelName in modelNames)
                {
                    services.AddKeyedSingleton(modelName, (sp, _) =>
                    {
                        return sp.CreateChatClientBuilder(openAiDeploymentName: modelName).Build();
                    });
                }
            }

            // backward compatibility
            var openAISettings = configuration.GetSection("AppSettings:Core:Azure:OpenAI").Get<OpenAISettings>();
            if (openAISettings != null && !string.IsNullOrWhiteSpace(openAISettings.LLMDeploymentName))
            {
                services.AddKeyedSingleton(openAISettings.LLMDeploymentName, (sp, _) =>
                {
                    return sp.CreateChatClientBuilder(openAiDeploymentName: openAISettings.LLMDeploymentName).Build();
                });
            }

            // register special case chat clients
            services
                .AddKeyedSingleton(Constants.FunctionInvocationChatClient, (sp, _) =>
                {
                    var client = sp.GetRequiredService<OpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                    return sp.CreateChatClientBuilder()
                        .UseFunctionInvocation(loggerFactory, x =>
                        {
                            x.IncludeDetailedErrors = true;
                        })
                        .Build();
                });

            // register chat client provider
            services.AddSingleton<IChatClientProvider, ChatClientProvider>();

            return services;
        }

        private static ChatClientBuilder CreateChatClientBuilder(this IServiceProvider serviceProvider, string? openAiDeploymentName = null)
        {
            // default to openai settings deployment if not provided
            openAiDeploymentName ??= serviceProvider.GetRequiredService<OpenAISettings>().LLMDeploymentName;

            var client = serviceProvider.GetRequiredService<OpenAIClient>();
            var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

            return new ChatClientBuilder(client.GetChatClient(openAiDeploymentName).AsIChatClient())
                .Use(next => new ReasoningChatClient(next))
                .UseTokenLogging(loggerFactory)
                .UseLogging(loggerFactory);
        }

        public static IServiceCollection ConfigureIEmbeddingGenerator(this IServiceCollection services, IConfiguration configuration)
        {
            var chatClientProviderSettings = configuration.GetSection("AppSettings:Core:ChatClientProvider").Get<ChatClientProviderSettings>();
            // backward compatibility
            var openAISettings = configuration.GetSection("AppSettings:Core:Azure:OpenAI").Get<OpenAISettings>();
            var embeddingModelName = !string.IsNullOrWhiteSpace(chatClientProviderSettings?.EmbeddingModelName)
                ? chatClientProviderSettings.EmbeddingModelName
                : openAISettings?.EmbeddingGeneratorDeploymentName;
            if (string.IsNullOrWhiteSpace(embeddingModelName))
            {
                return services;
            }

            return services.AddKeyedSingleton(embeddingModelName, (sp, _) =>
            {
                var openAISettings = sp.GetRequiredService<OpenAISettings>();
                var client = sp.GetRequiredService<AzureOpenAIClient>();

                return client.GetEmbeddingClient(embeddingModelName).AsIEmbeddingGenerator();
            });
        }
    }
}
