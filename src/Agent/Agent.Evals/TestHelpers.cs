using System.ClientModel;
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.AgentMemory;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Graph.Crawler.Metrics;
using Agent.Logging;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Agent.Prometheus.Services;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.IncidentHandlerAgent;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Agent.Tests.Common;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.Mocks.FunctionCalling;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;
using OpenTelemetry.Trace;

namespace Agent.Evals;

public static class TestHelpers
{
    public static HostApplicationBuilder BuildTestApp(out string? outLLMDeploymentName)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.LoadAppSettings(isDevelopment: true);
        builder.RegisterAppSettingsNoValidation<AppSettings>();

        var llmDeploymentName = builder.Configuration["AppSettings:Core:Azure:OpenAI:LLMDeploymentName"];

        if (string.IsNullOrEmpty(llmDeploymentName))
        {
            Console.WriteLine("Eval pipeline doesn't use appsettings. Using OpenAI API key and model from TestRunParameters.");

            var apiKey = builder.Configuration["OpenAIKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is missing. Pass it as a TestRunParameter.");
            }

            llmDeploymentName = builder.Configuration["OpenAIModel"];
            if (string.IsNullOrEmpty(llmDeploymentName))
            {
                throw new InvalidOperationException("OpenAI API model is missing. Pass it as a TestRunParameter.");
            }

            var aiEndpoint = builder.Configuration["OpenAIEndpoint"];
            if (string.IsNullOrEmpty(aiEndpoint))
            {
                throw new InvalidOperationException("OpenAI API endpoint is missing. Pass it as a TestRunParameter.");
            }

            builder.Services.AddSingleton(new AzureOpenAIClient(new Uri(aiEndpoint), new ApiKeyCredential(apiKey)));

        }
        else
        {
            Console.WriteLine("Eval pipeline is using appsettings. Please make sure you have proper values in appsettings.json.");
            builder.Services.ConfigureAzureOpenAIClient();
        }

        builder.Services.ConfigureIEmbeddingGenerator();
        builder.Services.AddLogging(builder =>
        {
            builder.AddConsole();
        });

        builder.Services.AddChatClient(sp => sp.GetRequiredService<AzureOpenAIClient>().GetChatClient(llmDeploymentName).AsIChatClient());

        builder.Services.ConfigureIEmbeddingGenerator();

        builder.Services.AddKeyedSingleton("function-invocation-enabled", (sp, _) =>
        {
            var client = sp.GetRequiredService<AzureOpenAIClient>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            return new ChatClientBuilder(client.GetChatClient(llmDeploymentName).AsIChatClient())
                .UseLogging(loggerFactory)
                .UseFunctionInvocation(loggerFactory, x =>
                {
                    x.IncludeDetailedErrors = true;
                })
                .Build();
        });

        builder.Services.AddKeyedSingleton("helper-agent-reasoning", (sp, _) =>
        {
            var client = sp.GetRequiredService<AzureOpenAIClient>();
            var loggerFactory = sp.GetRequiredService<ILoggerFactory>();

            return new ChatClientBuilder(client.GetChatClient(llmDeploymentName).AsIChatClient())
                .UseLogging(loggerFactory)
                .UseFunctionInvocation(loggerFactory, x =>
                {
                    x.IncludeDetailedErrors = true;
                })
                .Build();
        });

        outLLMDeploymentName = llmDeploymentName;

        return builder;
    }

    public static HostApplicationBuilder RegisterDefaultServices(this HostApplicationBuilder builder)
    {
        builder.Services.AddSingleton<IThreadOrchestrationManager, InMemoryThreadOrchestrationManager>();
        builder.Services.AddSingleton<IThreadRepository, InMemoryThreadRepository>();
        builder.Services.AddSingleton<IInstanceManagementRepository, InMemoryInstanceManagementRepository>();
        builder.Services.AddSingleton<ThreadService>();
        builder.Services.AddSingleton<SinkService>();
        // NOTE: use mock for teams plugin as we don't rely on teams for Agent Eval.
        builder.Services.AddSingleton(sp => new Mock<IPostToTeamsPlugin>().Object);

        return builder;
    }
    public static HostApplicationBuilder RegisterServicesForAgentFrameworkEval(this HostApplicationBuilder builder, JsonSerializerOptions? toolReplaySerializerOptions = null)
    {
        // Add HTTP client factory - required by various services
        builder.Services.AddHttpClient();

        // Add mock Azure services for testing
        builder.Services.AddSingleton(Mock.Of<Azure.Storage.Blobs.BlobServiceClient>());
        builder.Services.AddKeyedSingleton("agentMemoryBlobClient", (sp, _) => Mock.Of<Azure.Storage.Blobs.BlobServiceClient>());
        builder.Services.AddKeyedSingleton("agentMemoryAISearchClient", (sp, _) => Mock.Of<Azure.Search.Documents.SearchClient>());
        builder.Services.AddKeyedSingleton("agentMemoryIndexClient", (sp, _) => Mock.Of<Azure.Search.Documents.Indexes.SearchIndexClient>());
        builder.Services.AddKeyedSingleton("agentMemoryIndexerClient", (sp, _) => Mock.Of<Azure.Search.Documents.Indexes.SearchIndexerClient>());

        // Add mock agent memory client
        builder.Services.AddSingleton(Mock.Of<IAgentMemoryClient>());

        // Add mock Prometheus service
        builder.Services.AddSingleton(Mock.Of<IPrometheusQueryService>());
        builder.Services.AddSingleton(Mock.Of<Agent.Graph.Services.IPrometheusEndpointService>());

        builder.Services.AddSingleton(Mock.Of<IAzureMetricsClient>());

        // Add mock Kubernetes client factory
        builder.Services.AddSingleton(Mock.Of<IKubernetesClientFactory>());

        // Add mock ARM client factory
        builder.Services.AddSingleton(Mock.Of<IArmClientFactory>());

        // Add mock Crawler Trigger Service
        builder.Services.AddSingleton(Mock.Of<ICrawlerTriggerService>());

        // Add ArmHelper for ArmPlugin
        builder.Services.AddSingleton<ArmHelper>();

        // Add ActionSettings configuration
        builder.Services.AddSingleton<ActionSettings>(sp =>
        {
            return new ActionSettings
            {
                Mode = ActionMode.Review,
                Identity = "system"
            };
        });

        // Add AzureSettings configuration
        builder.Services.AddSingleton<AzureSettings>(sp =>
        {
            return new AzureSettings(); // Default empty settings for tests
        });

        // Add mock IHostEnvironment
        builder.Services.AddSingleton(Mock.Of<IHostEnvironment>());

        builder.Services.AddSingleton<IIncidentHandlerAgent, IncidentHandlerAgent>();
        builder.Services.AddSingleton<ThreadManagementService>();
        builder.Services.AddSingleton<IAgentInboundCommunicationService, InboundCommunicationService>();
        builder.Services.AddSingleton<IAgentRuntimeModifier<AgentContext>, AgentRuntimeModifier>();
        builder.Services.AddSingleton<IStreamingService>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>()
                .CreateLogger<MockStreamingService>();
            return new MockStreamingService(logger);
        });
        builder.Services.AddSingleton<IIncidentHandlerAgent, IncidentHandlerAgent>();
        builder.Services.AddSingleton<IAgentOutboundCommunicationService, OutboundCommunicationService>();
        builder.Services.AddSingleton<Agent.Plugins.Interface.IChartPlugin>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<Agent.Plugins.ChartPlugin>();
            var outboundService = sp.GetRequiredService<IAgentOutboundCommunicationService>();
            return new Agent.Plugins.ChartPlugin(logger, outboundService);
        });
        builder.Services.AddTransient<IAgent, MetaAgent>();
        builder.Services.AddSingleton<ChartPluginDefinition>();
        builder.Services.AddSingleton(Mock.Of<IAuthenticationService>());
        builder.Services.AddSingleton<ITitleGenerationService, TitleGenerationService>();

        builder.Services.AddSingleton<IGraphDBPlugin, GraphDBPlugin>();
        // builder.Services.AddSingleton<Agent.Plugins.Interface.IGraphDBPlugin>(sp => sp.GetRequiredService<GraphDBPlugin>());
        builder.Services.AddSingleton<GraphDBPluginDefinition>();
        builder.Services.AddSingleton<UserInteractionPluginDefinition>();
        builder.Services.AddSingleton<AgentControlFlowPluginDefinition>();

        builder.Services.AddSingleton<IReasoningLoopManager, ReasoningLoopManager>();
        builder.Services.AddSingleton<IReasoningLoopFactory, ReasoningLoopFactory>();

        builder.Services.AddSingleton(TracerProvider.Default.GetTracer("SREAgentTests"));
        builder.Services.AddSingleton(Mock.Of<CustomerLogger>());
        builder.Services.AddSingleton(Mock.Of<CustomerAuditLogger>());

        var agentModeString = builder.Configuration.GetSection("AppSettings:Core:Azure:Action:Mode").Get<string>();

        // This block is correct for conditionally registering the configurator
        if (string.Equals(agentModeString, "ReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IAgentModeConfigurator<AgentContext>, ReadOnlyAgentModeConfigurator<AgentContext>>();
        }
        else // Assuming this is your default/full access mode
        {
            builder.Services.AddSingleton<IAgentModeConfigurator<AgentContext>, DefaultAgentModeConfigurator<AgentContext>>();
        }

        builder.Services.AddSingleton<IToolFactory<AgentContext>>(sp =>
        {
            var inner = new ToolFactory<AgentContext>(
                logger: sp.GetRequiredService<ILogger<ToolFactory<AgentContext>>>(),
                serviceProvider: sp,
                assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.") == true));

            var replay = new ReplayToolFactory<AgentContext>(inner, toolReplaySerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return replay;
        });

        builder.Services.AddSingleton<IAgentFactory<AgentContext>, AgentFactory<AgentContext>>(sp =>
        {
            var modeConfigurator = sp.GetRequiredService<IAgentModeConfigurator<AgentContext>>();

            return new AgentFactory<AgentContext>(
                logger: sp.GetRequiredService<ILogger<AgentFactory<AgentContext>>>(),
                toolFactory: sp.GetRequiredService<IToolFactory<AgentContext>>(),
                assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.") == true),
                modeConfigurator: modeConfigurator,
                agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "AgentsV2"),
                commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "CommonPrompts"),
                commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "CommonTools"),
                promptStarters: [Core.Constants.SREAgentPromptStarter],
                promptEnders: [Core.Constants.SREAgentFinalInstructions],
                defaultOutputType: typeof(DefaultAgentOutput)
            );
        });

        // should be removed later - currently required because ThreadManagementService has code for handling UseAgentFramework=false
        builder.Services.AddSingleton<IAgentsFactory>(sp =>
        {
            return MetaAgentMock.GetMockedThirdPartAgentsFactory(
                graphDBPlugin: sp.GetRequiredService<GraphDBPlugin>()
                );
        });

        builder.Services.AddSingleton<ISearchEndpointService, SearchEndpointService>();

        builder.Services.AddSingleton<SearchHelper>();
        builder.Services.AddTransient<KubePluginDefinition>();
        builder.Services.AddTransient<IKubePlugin, KubePlugin>();
        builder.Services.AddTransient<SearchPluginDefinition>();
        builder.Services.AddSingleton(Mock.Of<IGraphDatabaseClient>());
        builder.Services.AddSingleton(Mock.Of<ISearchPlugin>());
        builder.Services.AddTransient<ArmPluginDefinition>();
        builder.Services.AddTransient<IArmPlugin, ArmPlugin>();

        // should be removed later - currently required because ThreadManagementService has code for handling UseAgentFramework=false
        // required because InboundCommunicationService has code for handling durable
        builder.ConfigureDurable();

        // Runtime–modifier for agent-mode switching
        builder.Services.AddSingleton<IAgentRuntimeModifier<AgentContext>, AgentRuntimeModifier>();

        // Search endpoint & helper (document-retrieval support)
        builder.Services.AddSearchEndpointHttpClient();
        builder.Services.AddSingleton<ISearchEndpointService, SearchEndpointService>();
        builder.Services.AddSingleton<SearchHelper>();

        // Agent-memory (disabled ➜ dummy implementation)
        builder.Services.AddSingleton<IAgentMemoryClient, DummyAgentMemoryClient>();
        builder.Services.AddSingleton(Mock.Of<ISearchIndexService>());

        return builder;
    }

    public static ChatResponse? GetChatResponseForUser(this ChatMessage msg)
    {
        var response = msg switch
        {
            _ when msg.Role == ChatRole.Assistant && !string.IsNullOrEmpty(msg.Text) => new ChatResponse(msg),
            _ when msg.Contents.OfType<FunctionCallContent>().SingleOrDefault() is { Name: "NotifyUser" } functionCall =>
                new ChatResponse(new ChatMessage(ChatRole.Assistant, functionCall.Arguments["message"].ToString())),
            _ => null
        };

        return response;
    }

    public static void WriteMessages(this TestContext testContext, IEnumerable<ChatMessage> chatMessages)
    {
        foreach (var message in chatMessages)
        {
            if (string.IsNullOrEmpty(message.Text))
            {
                testContext.WriteLine($"{System.Text.Json.JsonSerializer.Serialize(message.Contents)}");
            }
            else
            {
                testContext.WriteLine($"[{message.Role}] {message.Text}");
            }

        }
    }

    public static int GetIterationCount(int defaultValue)
    {
        string? iterationCountEnv = Environment.GetEnvironmentVariable("IterationCount");
        if (int.TryParse(iterationCountEnv, out int parsedIterations))
        {
            return parsedIterations;
        }
        else
        {
            return defaultValue;
        }
    }

    public static TestHost InitializeTestHost()
    {
        var builder = BuildTestApp(out var _);
        builder.RegisterDefaultServices();
        builder.RegisterServicesForAgentFrameworkEval();
        var host = builder.Build();
        return TestHost.Create(host);
    }

    public static bool IsAgentMemoryEnabled(this HostApplicationBuilder builder)
    {
        var agentMemorySettings = builder.Configuration.GetSection("AppSettings:Core:AgentMemory").Get<AgentMemorySettings>();
        return agentMemorySettings?.Enabled ?? false;
    }

    public static void ConfigureAgentMemory(this HostApplicationBuilder builder)
    {
        builder.Services.AddAgentMemory(
            enableAgentMemory: builder.IsAgentMemoryEnabled());
    }
}

class MockStreamingService : IStreamingService
{
    private readonly ILogger<MockStreamingService> _logger;

    public MockStreamingService(ILogger<MockStreamingService> logger)
    {
        _logger = logger;
    }

    public Task StreamChatResponseUpdateAsync(Guid threadId, ChatResponseUpdate update, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Streaming message for thread {ThreadId}: {Message}",
            threadId, update.Text);
        return Task.CompletedTask;
    }

    public Task StreamMessageAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Streaming message for thread {ThreadId} with type {Type}: {Message}",
            threadId, type, message);
        return Task.CompletedTask;
    }

    public Task StreamThreadUpdateAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Thread update for thread {ThreadId} with type {Type}: {Message}",
            threadId, type, message);
        return Task.CompletedTask;
    }
}
