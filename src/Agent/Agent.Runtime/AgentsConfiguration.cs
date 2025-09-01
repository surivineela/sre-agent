// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Clients.Chat;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Azure.Core;
using Azure.Core.Pipeline;
using System.ClientModel.Primitives;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Agent.Logging;

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
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
                    // use a typed logger so logs are categorized under the policy type
                    var logger = loggerFactory.CreateLogger<AzureOpenAILoggingPolicy>();

                    // create options with a custom retry policy that logs 429 responses
                    var options = new AzureOpenAIClientOptions();
                    // register a pipeline policy that logs 429 responses
                    options.AddPolicy(new AzureOpenAILoggingPolicy(logger), PipelinePosition.PerCall);

                    // TODO: remove api key after CP is deployed
                    if (!string.IsNullOrEmpty(openAISettings.ApiKey))
                    {
                        return new AzureOpenAIClient(
                            endpoint: new Uri(openAISettings.Endpoint),
                            credential: new System.ClientModel.ApiKeyCredential(openAISettings.ApiKey),
                            options: options
                        );
                    }
                    else
                    {
                        var authService = sp.GetRequiredService<IAuthenticationService>();
                        var cred = authService.GetAzureOpenAICredential();
                        return new AzureOpenAIClient(
                            endpoint: new Uri(openAISettings.Endpoint),
                            credential: cred,
                            options: options
                        );
                    }
                });
        }

        // Pipeline policy that logs 429 responses using the project's structured logging helper
    internal class AzureOpenAILoggingPolicy : System.ClientModel.Primitives.PipelinePolicy
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

        public static IServiceCollection ConfigureIChatClient(this IServiceCollection services)
        {
            return services
                .AddSingleton(sp =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                    return new ChatClientBuilder(client.GetChatClient(openAISettings.LLMDeploymentName).AsIChatClient())
                        .Use(next => new ReasoningChatClient(next))
                        .UseTokenLogging(loggerFactory)
                        .UseLogging(loggerFactory)
                        .Build();
                })
                .AddKeyedSingleton("function-invocation-enabled", (sp, _) =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var openAISettings = sp.GetRequiredService<OpenAISettings>();
                    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

                    return new ChatClientBuilder(client.GetChatClient(openAISettings.LLMDeploymentName).AsIChatClient())
                        .Use(next => new ReasoningChatClient(next))
                        .UseTokenLogging(loggerFactory)
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
                        .Use(next => new ReasoningChatClient(next))
                        .UseTokenLogging(loggerFactory)
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
                        .Use(next => new ReasoningChatClient(next))
                        .UseTokenLogging(loggerFactory)
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
