// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Configuration;
using Agent.Core;
using Agent.Core.Models;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Agent.Plugins.Definitions;
using Agent.Core.Interfaces;
using Agent.Data.DataModels;

namespace Agent.Runtime
{
    public static class AgentsConfigurationExtensions
    {
        public static IServiceCollection ConfigureIChatCompletionService(this IServiceCollection services)
        {
            return services
                .AddSingleton((Func<IServiceProvider, IChatCompletionService>)(sp =>
                {
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();

                    // TODO: remove api key after CP is deployed
                    if (!string.IsNullOrEmpty(openAISettings.ApiKey))
                    {
                        return (IChatCompletionService)new AzureOpenAIChatCompletionService(
                            deploymentName: openAISettings.LLMDeploymentName,
                            endpoint: openAISettings.Endpoint,
                            apiKey: openAISettings.ApiKey
                        );
                    }
                    else
                    {
                        var authService = sp.GetRequiredService<IAuthenticationService>();
                        var cred = authService.GetAzureOpenAICredential();
                        return (IChatCompletionService)new AzureOpenAIChatCompletionService(
                            deploymentName: openAISettings.LLMDeploymentName,
                            endpoint: openAISettings.Endpoint,
                            cred
                        );
                    }
                }));
        }

        public static IServiceCollection ConfigureAzureOpenAIClient(this IServiceCollection services)
        {
            return services
                .AddSingleton((Func<IServiceProvider, AzureOpenAIClient>)(sp =>
                {
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();

                    // TODO: remove api key after CP is deployed
                    if (!string.IsNullOrEmpty(openAISettings.ApiKey))
                    {
                        return new AzureOpenAIClient(
                            endpoint: new Uri(openAISettings.Endpoint),
                            credential: new System.ClientModel.ApiKeyCredential(openAISettings.ApiKey)
                        );
                    }
                    else
                    {
                        var authService = sp.GetRequiredService<IAuthenticationService>();
                        var cred = authService.GetAzureOpenAICredential();
                        return new AzureOpenAIClient(
                            endpoint: new Uri(openAISettings.Endpoint),
                            credential: cred
                        );
                    }
                }));
        }

        public static IServiceCollection ConfigureIChatClient(this IServiceCollection services)
        {
            return services
                .AddSingleton<IChatClient>((Func<IServiceProvider, IChatClient>)(sp =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                    return new ChatClientBuilder(client.AsChatClient(openAISettings.LLMDeploymentName))
                        .UseLogging(loggerFactory)
                        .Build();
                }))
                .AddKeyedSingleton<IChatClient>("function-invocation-enabled", (sp, _) =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                    return new ChatClientBuilder(client.AsChatClient(openAISettings.LLMDeploymentName))
                        .UseLogging(loggerFactory)
                        .UseFunctionInvocation(loggerFactory, x =>
                        {
                            x.IncludeDetailedErrors = true;
                        })
                        .Build();
                })
                .AddKeyedSingleton<IChatClient>("subagentv2-reasoning", (sp, _) =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var settings = sp.GetRequiredService<InstanceManagementSettings>();

                    return new ChatClientBuilder(client.AsChatClient(openAISettings.LLMDeploymentName))
                        .UseLogging(loggerFactory)
                        .UseFunctionInvocation(loggerFactory, x =>
                        {
                            x.IncludeDetailedErrors = true;
                            x.MaximumIterationsPerRequest = settings.ReasoningChatClientMaximumIterations;
                        })
                        .Build();
                });
        }

        public static IServiceCollection ConfigureIEmbeddingGenerator(this IServiceCollection services)
        {
            return services
                .AddSingleton<IEmbeddingGenerator<string, Embedding<float>>>((Func<IServiceProvider, IEmbeddingGenerator<string, Embedding<float>>>)(sp =>
                {
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var client = sp.GetRequiredService<AzureOpenAIClient>();

                    return client.AsEmbeddingGenerator(openAISettings.EmbeddingGeneratorDeploymentName);
                }));
        }
    }
}
