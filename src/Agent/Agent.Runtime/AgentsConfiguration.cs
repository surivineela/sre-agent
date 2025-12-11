// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ClientModel;
using System.ClientModel.Primitives;
using System.Net.Http.Headers;
using System.Reflection;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Framework;
using Anthropic;
using Anthropic.Services;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using OpenAI;

namespace Agent.Runtime;

public static class AgentsConfigurationExtensions
{
    private static readonly string UserAgent = GetUserAgent();

    private static string GetUserAgent()
    {
        var version = Assembly.GetExecutingAssembly().GetName().Version;
        var versionString = version != null ? $"{version.Major}.{version.Minor}.{version.Build}" : "1.0.0";
        var productInfo = new ProductInfoHeaderValue("azure-sre-agent", versionString);
        return productInfo.ToString();
    }
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
                    RetryPolicy = new ClientRetryPolicy(2),
                    UserAgentApplicationId = UserAgent
                };

                // register a pipeline policy that logs LLM Http request details
                options.AddPolicy(new AzureOpenAILoggingPolicy(logger), PipelinePosition.PerTry);

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
                        RetryPolicy = new ClientRetryPolicy(2),
                        UserAgentApplicationId = UserAgent
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
            var threadId = Core.ToolStatic.AsyncLocalThreadId.Value.ToString();
            long requestContentLength = -1;
            message.Request.Content?.TryComputeLength(out requestContentLength);
            var startTimestamp = DateTime.UtcNow;

            try
            {
                // Invoke next policy in pipeline
                ProcessNext(message, pipeline, index);

                var responseContentLength = -1;
                var endTimestamp = DateTime.UtcNow;
                ExecuteAndSwallowException(() =>
                {
                    responseContentLength = message.Response?.Content?.Length ?? -1;
                }
                );
                ExecuteAndSwallowException(() =>
                {
                    // Log detailed model request information
                    LogModelRequestDetails(message, requestContentLength, responseContentLength, startTimestamp, endTimestamp, threadId);
                });
            }
            catch (TaskCanceledException) when (!message.CancellationToken.IsCancellationRequested)
            {
                ExecuteAndSwallowException(() =>
                {
                    LogModelRequestDetails(message, requestContentLength, -1, startTimestamp, DateTime.UtcNow, threadId);
                });
                throw;
            }
            catch (TimeoutException)
            {
                ExecuteAndSwallowException(() =>
                    LogModelRequestDetails(message, requestContentLength, -1, startTimestamp, DateTime.UtcNow, threadId)
                );
                throw;
            }
            catch (OperationCanceledException) when (!message.CancellationToken.IsCancellationRequested)
            {
                ExecuteAndSwallowException(() =>
                    LogModelRequestDetails(message, requestContentLength, -1, startTimestamp, DateTime.UtcNow, threadId)
                );
                throw;
            }

            ExecuteAndSwallowException(() =>
            {
                if (message.Response != null && message.Response.Status >= 400)
                {
                    // Log detailed model request information for errors too
                    LogModelRequestDetails(message, requestContentLength, -1, startTimestamp, DateTime.UtcNow, threadId);
                }
            });
        }

        public override async ValueTask ProcessAsync(PipelineMessage message, IReadOnlyList<PipelinePolicy> pipeline, int index)
        {
            var threadId = Core.ToolStatic.AsyncLocalThreadId.Value.ToString();
            long requestContentLength = -1;
            message.Request.Content?.TryComputeLength(out requestContentLength);
            var startTimestamp = DateTime.UtcNow;
            LogModelRequestDetails(message, requestContentLength, -1, startTimestamp, DateTime.UtcNow, threadId);
            try
            {
                // Invoke next policy in pipeline
                await ProcessNextAsync(message, pipeline, index).ConfigureAwait(false);

                var responseContentLength = -1;
                var endTimestamp = DateTime.UtcNow;
                ExecuteAndSwallowException(() =>
                {
                    responseContentLength = message.Response?.Content?.Length ?? -1;
                }
                );
                ExecuteAndSwallowException(() =>
                {
                    // Log detailed model request information
                    LogModelRequestDetails(message, requestContentLength, responseContentLength, startTimestamp, endTimestamp, threadId);
                });
            }
            catch (TaskCanceledException) when (!message.CancellationToken.IsCancellationRequested)
            {
                ExecuteAndSwallowException(() =>
                    LogModelRequestDetails(message, requestContentLength, -1, startTimestamp, DateTime.UtcNow, threadId)
                );
                throw;
            }
            catch (TimeoutException)
            {
                ExecuteAndSwallowException(() =>
                    LogModelRequestDetails(message, requestContentLength, -1, startTimestamp, DateTime.UtcNow, threadId)
                );
                throw;
            }
            catch (OperationCanceledException) when (!message.CancellationToken.IsCancellationRequested)
            {
                ExecuteAndSwallowException(() =>
                    LogModelRequestDetails(message, requestContentLength, -1, startTimestamp, DateTime.UtcNow, threadId)
                );
                throw;
            }

            ExecuteAndSwallowException(() =>
                {
                    if (message.Response != null && message.Response.Status >= 400)
                    {
                        // Log detailed model request information for errors too
                        LogModelRequestDetails(message, requestContentLength, -1, startTimestamp, DateTime.UtcNow, threadId);
                    }
                }
            );
        }

        private void ExecuteAndSwallowException(Action action)
        {
            try
            {
                action();
            }
            catch
            {
                // Swallow logging errors so we don't affect the caller
            }
        }

        private void LogModelRequestDetails(PipelineMessage message, long requestContentLength, long responseContentLength, DateTime startTimestamp, DateTime endTimestamp, string threadId)
        {
            try
            {
                var path = message.Request.Uri?.AbsolutePath ?? string.Empty;
                var statusCode = message.Response?.Status ?? 0;
                var modelName = ExtractModelName(message);
                var responseHeader = SerializeResponseHeaders(message.Response);
                var requestHeader = SerializeRequestHeaders(message.Request);
                var latency = (long)(endTimestamp - startTimestamp).TotalMilliseconds;
                var requestSize = requestContentLength;
                var responseSize = responseContentLength;
                var remainingRequests = ExtractRateLimitHeader(message.Response, "x-ratelimit-remaining-requests");
                var remainingTokens = ExtractRateLimitHeader(message.Response, "x-ratelimit-remaining-tokens");

                _logger.LogModelRequest(
                    path: path,
                    statusCode: statusCode,
                    modelName: modelName,
                    hostName: message.Request.Uri?.Host ?? string.Empty,
                    responseHeader: responseHeader,
                    requestHeader: requestHeader,
                    latency: latency,
                    requestSize: requestSize,
                    responseSize: responseSize,
                    remainingRequests: remainingRequests,
                    remainingTokens: remainingTokens,
                    threadId: threadId);
            }
            catch
            {
                // Swallow any errors in logging to prevent affecting the pipeline
            }
        }

        private string ExtractModelName(PipelineMessage message)
        {
            try
            {
                // Try to extract model name from the request path (e.g., /openai/deployments/{model}/chat/completions)
                var path = message.Request.Uri?.AbsolutePath ?? string.Empty;
                var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);

                // Look for deployment name in typical Azure OpenAI path structure
                for (var i = 0; i < segments.Length - 1; i++)
                {
                    if (segments[i].Equals("deployments", StringComparison.OrdinalIgnoreCase))
                    {
                        return segments[i + 1];
                    }
                }
            }
            catch
            {
                // Fall through to return empty string
            }

            return string.Empty;
        }

        private string SerializeResponseHeaders(PipelineResponse? response)
        {
            try
            {
                if (response == null)
                {
                    return string.Empty;
                }

                var headers = new Dictionary<string, string>();
                foreach (var header in response.Headers)
                {
                    headers[header.Key] = header.Value;
                }

                return WebJsonSerializer.Serialize(headers);
            }
            catch
            {
                return string.Empty;
            }
        }

        private string SerializeRequestHeaders(PipelineRequest? request)
        {
            try
            {
                if (request == null)
                {
                    return string.Empty;
                }

                var headers = new Dictionary<string, string>();
                foreach (var header in request.Headers)
                {
                    // Exclude sensitive headers
                    if (!header.Key.Equals("api-key", StringComparison.OrdinalIgnoreCase) &&
                        !header.Key.Equals("authorization", StringComparison.OrdinalIgnoreCase))
                    {
                        headers[header.Key] = header.Value;
                    }
                }

                return WebJsonSerializer.Serialize(headers);
            }
            catch
            {
                return string.Empty;
            }
        }

        private long ExtractRateLimitHeader(PipelineResponse? response, string headerName)
        {
            try
            {
                if (response == null)
                {
                    return 0;
                }

                if (response.Headers.TryGetValue(headerName, out var value) &&
                    long.TryParse(value, out var result))
                {
                    return result;
                }
            }
            catch
            {
                // Fall through to return 0
            }

            return 0;
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
            var modelsByProvider = chatClientProviderSettings.ModelNames
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(m => m.Trim())
                .Where(m => !string.IsNullOrWhiteSpace(m))
                .Distinct()
                .GroupBy(m => m.Contains("claude") ? "claude" : m.Contains("gpt") ? "gpt" : "other")
                .ToDictionary(g => g.Key, g => g.ToList());

            if (modelsByProvider.TryGetValue("gpt", out var openaiModels))
            {
                foreach (var modelName in openaiModels)
                {
                    services.AddKeyedSingleton(modelName, (sp, _) =>
                    {
                        return sp.CreateChatClientBuilder(openAiDeploymentName: modelName).Build();
                    });
                }
            }

            if (modelsByProvider.TryGetValue("claude", out var anthropicModels))
            {
                ConfigureAnthropicChatClients(services, configuration, anthropicModels);
            }

            if (modelsByProvider.TryGetValue("other", out var unsupportedModels))
            {
                throw new InvalidOperationException($"Unsupported model(s) found in ChatClientProvider.ModelNames: {string.Join(", ", unsupportedModels)}");
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

#pragma warning disable OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.
    public static ChatClientBuilder CreateChatClientBuilder(this IServiceProvider serviceProvider, string? openAiDeploymentName = null)
    {
        // default to openai settings deployment if not provided
        openAiDeploymentName ??= serviceProvider.GetRequiredService<OpenAISettings>().LLMDeploymentName;

        var experimentalSettings = serviceProvider.GetRequiredService<ExperimentalSettings>();
        var useResponsesApi = experimentalSettings.UseResponsesApi;

        // load experiments to see if Responses API is enabled via experiment
        var experimentLoader = serviceProvider.GetRequiredService<IExperimentLoader>();
        if (experimentLoader.IsFeatureFlagEnabled(Constants.FeatureFlags.EnableResponsesApi))
        {
            useResponsesApi = true;
        }

        var client = serviceProvider.GetRequiredService<OpenAIClient>();

        // Pick OpenAIResponseClient or ChatClient based on experimental settings
        var chatClient = useResponsesApi
            ? client.GetOpenAIResponseClient(openAiDeploymentName).AsIChatClient()
            : client.GetChatClient(openAiDeploymentName).AsIChatClient();

        var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

        return new ChatClientBuilder(chatClient)
            .Use(next => new ReasoningChatClient(next, new OpenAIReasoningChatClientOptions(UseResponsesApi: useResponsesApi)))
            .UseTokenLogging(loggerFactory)
            .UseLogging(loggerFactory);
    }
#pragma warning restore OPENAI001 // Type is for evaluation purposes only and is subject to change or removal in future updates. Suppress this diagnostic to proceed.

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

    public static void ConfigureAnthropicChatClients(this IServiceCollection services, IConfiguration configuration, List<string> anthropicModels)
    {
        var anthropicSettings = configuration.GetSection("AppSettings:Core:Anthropic").Get<AnthropicSettings>();
        if (anthropicSettings is not null)
        {
            foreach (var modelName in anthropicModels)
            {
                _ = services.AddKeyedSingleton(modelName, (sp, _) =>
                {
                    var authService = sp.GetRequiredService<IAuthenticationService>();
                    var httpClient = new HttpClient();
                    bool interleavedThinkingEnabled = anthropicSettings.InterleavedThinkingEnabled && anthropicSettings.ExtendedThinkingEnabled;
                    // https://platform.claude.com/docs/en/build-with-claude/structured-outputs
                    if (!interleavedThinkingEnabled)
                    {
                        httpClient.DefaultRequestHeaders.Add("anthropic-beta", "structured-outputs-2025-11-13");
                    }
                    else
                    {
                        // https://platform.claude.com/docs/en/build-with-claude/extended-thinking#interleaved-thinking
                        httpClient.DefaultRequestHeaders.Add("anthropic-beta", "structured-outputs-2025-11-13,interleaved-thinking-2025-05-14");
                    }

                    var options = new Anthropic.Core.ClientOptions
                    {
                        BaseUrl = new Uri(anthropicSettings.BaseUrl),
                        MaxRetries = anthropicSettings.MaxRetries,
                        HttpClient = httpClient,
                    };

                    var client = string.IsNullOrEmpty(anthropicSettings.ApiKey) switch
                    {
                        false => new BetaService(new AnthropicClient(options)).WithOptions(o =>
                        {
                            o.APIKey = anthropicSettings.ApiKey;
                            return o;
                        }),
                        true => new BetaService(new AnthropicTokenCredentialClient(authService.GetAzureAnthropicCredential(), options)),
                    };

                    return new ChatClientBuilder(client.AsIChatClient(defaultModelId: modelName, defaultMaxOutputTokens: anthropicSettings.MaxOutputTokens))
                        .Use(next => new ReasoningChatClient(next, new AnthropicReasoningChatClientOptions(
                            ModelId: modelName,
                            ExtendedThinkingEnabled: anthropicSettings.ExtendedThinkingEnabled,
                            MaxOutputTokens: anthropicSettings.MaxOutputTokens,
                            ThinkingTokenBudget: anthropicSettings.ThinkingBudgetTokens)))
                        .UseTokenLogging(sp.GetRequiredService<ILoggerFactory>())
                        .UseLogging(sp.GetRequiredService<ILoggerFactory>())
                        .Build();
                });
            }

        }
    }
}
