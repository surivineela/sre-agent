// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

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
                .AddSingleton(sp =>
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
                });
        }

        public static IServiceCollection ConfigureIChatClient(this IServiceCollection services)
        {
            return services
                .AddSingleton(sp =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                    return new ChatClientBuilder(client.GetChatClient(openAISettings.LLMDeploymentName).AsIChatClient())
                        .UseLogging(loggerFactory)
                        .Build();
                })
                .AddKeyedSingleton("function-invocation-enabled", (sp, _) =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                    return new ChatClientBuilder(client.GetChatClient(openAISettings.LLMDeploymentName).AsIChatClient())
                        .UseLogging(loggerFactory)
                        .UseFunctionInvocation(loggerFactory, x =>
                        {
                            x.IncludeDetailedErrors = true;
                        })
                        .Build();
                })
                .AddKeyedSingleton("helper-agent-reasoning", (sp, _) =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                    return new ChatClientBuilder(client.GetChatClient(openAISettings.LLMDeploymentName).AsIChatClient())
                        .UseLogging(loggerFactory)
                        .UseFunctionInvocation(loggerFactory, x =>
                        {
                            x.IncludeDetailedErrors = true;
                            x.MaximumIterationsPerRequest = 20;
                        })
                        .Build();
                })
                .AddKeyedSingleton("subagentv2-reasoning", (sp, _) =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    var settings = sp.GetRequiredService<InstanceManagementSettings>();

                    return new ChatClientBuilder(client.GetChatClient(openAISettings.LLMDeploymentName).AsIChatClient())
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
                .AddSingleton(sp =>
                {
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var client = sp.GetRequiredService<AzureOpenAIClient>();

                    return client.GetEmbeddingClient(openAISettings.EmbeddingGeneratorDeploymentName).AsIEmbeddingGenerator();
                });
        }
    }
}
