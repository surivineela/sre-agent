using System.ClientModel;
using System.Text.Json;
using Agent.Core.Clients.Chat;
using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Plugins.Definitions;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.AgentMemory;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Framework.Interfaces;
using Agent.Graph.Crawler.Metrics;
using Agent.Logging;
using Agent.Plugins;
using Agent.Plugins.Clients;
using Agent.Plugins.Definitions;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Agent.Plugins.Services.Interfaces;
using Agent.Prometheus.Services;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.IncidentHandlerAgent;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.Mocks.FunctionCalling;
using Azure.AI.OpenAI;
using Microsoft.AspNetCore.Hosting;
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

        builder.Services.AddChatClient(sp => sp.GetRequiredService<AzureOpenAIClient>().GetChatClient(llmDeploymentName).AsIChatClient()).Use(next => new ReasoningChatClient(next));

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

    public static HostApplicationBuilder RegisterFirstPartyServices(this HostApplicationBuilder builder)
    {
        // Register ACA First Party tools

        // Add mock IHostEnvironment
        builder.Services.AddSingleton(Mock.Of<IWebHostEnvironment>());
        builder.Services.AddSingleton<IKeyVaultService, KeyVaultService>();

        // Add mock implementations for missing dependencies
        builder.Services.AddSingleton(Mock.Of<IKustoPlugin>());
        builder.Services.AddSingleton(Mock.Of<ITimePlugin>());

        builder.Services.AddSingleton<IContainerAppIcMPlugin, ContainerAppIcMPlugin>();
        // Tests don't need a real ICM API client - provide a mock so plugins depending on it can be constructed
        builder.Services.AddSingleton(Mock.Of<FirstPartyAgent.Core.Services.IICMAPIClient>());
        // Also provide a mock for the Agent.Core IICMAPIClient used by first-party plugins
        builder.Services.AddSingleton(Mock.Of<Agent.Core.Services.IICMAPIClient>());
        builder.Services.AddSingleton<ICMWorkflowClient>();
        builder.Services.AddSingleton(Mock.Of<IICMAPIClient>());
        builder.Services
            .AddTransient<RCAContainerAppsIngressPluginDefinition>()
            .AddTransient<RCAContainerAppAspirePluginDefinition>()
            .AddTransient<RCAContainerAppCorednsPluginDefinition>()
            .AddTransient<RCAContainerAppOutboundConnectionPluginDefinition>()
            .AddTransient<RCAContainerAppsManagedEnvironmentPluginDefinition>()
            .AddTransient<RCAContainerAppsManagedClusterPluginDefinition>()
            .AddTransient<RCAContainerAppsJobsPluginDefinition>()
            .AddTransient<RCAContainerAppsSessionsPluginDefinition>()
            .AddTransient<RCAContainerAppCustomerLogsPluginDefinition>()
            .AddTransient<RCAContainerAppIcMPluginDefinition>()
            .AddTransient<RCAContainerAppCustomerMetricsPluginDefinition>()
            .AddTransient<RCAContainerAppQuotaPluginDefinition>()
            .AddTransient<RCAContainerAppRevisionPluginDefinition>()
            .AddSingleton(Mock.Of<IKustoDashboardPlugin>())
            .AddTransient(sp => Mock.Of<IAzureDocSearchPlugin>())
            .AddTransient(sp => Mock.Of<IAzureSearchClient>())
            .AddTransient(sp =>
            {
                var mock = new Mock<AzureDocSearchPlugin>(Mock.Of<ILogger<AzureDocSearchPlugin>>(), Mock.Of<IAzureSearchClient>());
                return mock.Object;
            })
            .AddTransient<RCAContainerAppResourceCheckPluginDefinition>()
            .AddTransient<RCAContainerAppResourceSearchPluginDefinition>()
            .AddTransient<RCAContainerAppsSwiftNetworkContainerPluginDefinition>()
            .AddTransient<RCAContainerAppPlatformUpgradesPluginDefinition>();

        return builder;
    }

    public static async Task<HostApplicationBuilder> RegisterServicesForAgentFrameworkEval(this HostApplicationBuilder builder, JsonSerializerOptions? toolReplaySerializerOptions = null)
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

        // Add CrawlerSettings configuration
        builder.Services.AddSingleton<CrawlerSettings>(sp =>
        {
            return new CrawlerSettings
            {
                TenantId = "00000000-0000-0000-0000-000000000000", // Test tenant ID
                CrawlRoots = "",
                Identity = "system",
                MaxParallelism = 1
            };
        });

        // Add AzureResourceGraphClient
        builder.Services.AddSingleton<Agent.Graph.Crawler.ARM.AzureResourceGraphClient>();

        // Add mock Crawler Trigger Service
        builder.Services.AddSingleton(Mock.Of<ICrawlerTriggerService>());

        // Add mock Session Pool Service
        builder.Services.AddSingleton(Mock.Of<ISessionPoolService>());

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
        builder.Services.AddSingleton<IIncidentStatusMetricsService, IncidentStatusMetricsService>();
        builder.Services.AddSingleton<IExtendedAgentRepository, InMemoryExtendedAgentRepository>();
        builder.Services.AddSingleton<IIncidentRepository, InMemoryIncidentRepository>();
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
        builder.Services.AddSingleton<IChartPlugin>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>().CreateLogger<ChartPlugin>();
            var outboundService = sp.GetRequiredService<IAgentOutboundCommunicationService>();
            return new ChartPlugin(logger, outboundService);
        });
        builder.Services.AddTransient<IAgent, MetaAgent>();
        builder.Services.AddSingleton<ChartPluginDefinition>();
        builder.Services.AddSingleton(Mock.Of<IAuthenticationService>());
        builder.Services.AddSingleton<ITitleGenerationService, TitleGenerationService>();

        builder.Services.AddSingleton<IGraphDBPlugin, GraphDBPlugin>();
        builder.Services.AddSingleton<GraphDBPluginDefinition>();
        builder.Services.AddSingleton(Mock.Of<IGraphDatabaseClient>());
        builder.Services.AddSingleton<UserInteractionPluginDefinition>();
        builder.Services.AddSingleton<AgentControlFlowPluginDefinition>();
        builder.Services.AddSingleton<AgentReasoningControlFlowPluginDefinition>();

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
                    .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.") == true),
                extensibilityLoader: sp.GetRequiredService<IExtensibilityLoader>());

            var replay = new ReplayToolFactory<AgentContext>(inner, toolReplaySerializerOptions ?? new JsonSerializerOptions(JsonSerializerDefaults.Web));
            return replay;
        });
        builder.Services.AddSingleton<IExtensibilityLoader, ExtensibilityLoader>();
        using var bootstrapServiceProvider = builder.Services.BuildServiceProvider();
        var modeConfigurator = bootstrapServiceProvider.GetRequiredService<IAgentModeConfigurator<AgentContext>>();
        var extensionLoader = bootstrapServiceProvider.GetRequiredService<IExtensibilityLoader>();
        var factory = await AgentFactory<AgentContext>.CreateAsync(
                logger: bootstrapServiceProvider.GetRequiredService<ILogger<AgentFactory<AgentContext>>>(),
                toolFactory: bootstrapServiceProvider.GetRequiredService<IToolFactory<AgentContext>>(),
                assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.") == true),

                modeConfigurator: modeConfigurator,
                agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "AgentsV2"),
                commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "CommonPrompts"),
                commonToolsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "CommonTools"),
                promptStarters: [Core.Constants.SREAgentPromptStarter],
                promptEnders: [Core.Constants.SREAgentFinalInstructions],
                defaultOutputType: typeof(DefaultAgentOutput),
                extensibiltyLoader: extensionLoader
            );

        builder.Services.AddSingleton<IAgentFactory<AgentContext>>(factory);
       

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
        builder.Services.AddSingleton(Mock.Of<ISearchPlugin>());
        builder.Services.AddTransient<ArmPluginDefinition>();
        builder.Services.AddTransient<IArmPlugin, ArmPlugin>();

        // Runtime–modifier for agent-mode switching
        builder.Services.AddSingleton<IAgentRuntimeModifier<AgentContext>, AgentRuntimeModifier>();

        // Search endpoint & helper (document-retrieval support)
        builder.Services.AddSearchEndpointHttpClient();
        builder.Services.AddSingleton<ISearchEndpointService, SearchEndpointService>();
        builder.Services.AddSingleton<SearchHelper>();

        // Agent-memory (disabled ➜ dummy implementation)
        builder.Services.AddSingleton<IAgentMemoryClient, DummyAgentMemoryClient>();
        builder.Services.AddSingleton(Mock.Of<ISearchIndexService>());

        // FirstParty services
        builder.RegisterFirstPartyServices();

        return builder;
    }

    public static ChatResponse? GetChatResponseForUser(this ChatMessage msg)
    {
        var response = msg switch
        {
            _ when msg.Role == ChatRole.Assistant && !string.IsNullOrEmpty(msg.Text) => new ChatResponse(msg),
            _ when msg.Contents.OfType<FunctionCallContent>().SingleOrDefault() is { Name: "NotifyUser" } functionCall =>
                new ChatResponse(
                    new ChatMessage(
                        ChatRole.Assistant,
                        functionCall?.Arguments != null &&
                        functionCall.Arguments.TryGetValue("message", out var message)
                            ? message?.ToString() ?? string.Empty
                            : string.Empty
                    )
                ),
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

    public static async Task<TestHost> InitializeTestHost()
    {
        var builder = BuildTestApp(out var _);
        builder.RegisterDefaultServices();
        await builder.RegisterServicesForAgentFrameworkEval();
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

    /// <summary>
    /// Compares function call arguments with smart handling for command-line arguments
    /// </summary>
    public static bool AreArgumentsEquivalent(object expected, object actual)
    {
        try
        {
            var expectedJson = JsonSerializer.Serialize(expected, new JsonSerializerOptions { WriteIndented = false });
            var actualJson = JsonSerializer.Serialize(actual, new JsonSerializerOptions { WriteIndented = false });

            // First try exact match
            if (expectedJson == actualJson) return true;

            // Parse as JSON objects for smart comparison
            using var expectedDoc = JsonDocument.Parse(expectedJson);
            using var actualDoc = JsonDocument.Parse(actualJson);

            var expectedRoot = expectedDoc.RootElement;
            var actualRoot = actualDoc.RootElement;

            // If both are objects, compare each property
            if (expectedRoot.ValueKind == JsonValueKind.Object && actualRoot.ValueKind == JsonValueKind.Object)
            {
                // Check if all expected properties exist in actual
                foreach (var expectedProp in expectedRoot.EnumerateObject())
                {
                    if (!actualRoot.TryGetProperty(expectedProp.Name, out var actualProp))
                        return false;

                    // Special handling for "command" property
                    if (expectedProp.Name.Equals("command", StringComparison.OrdinalIgnoreCase))
                    {
                        var expectedCommand = expectedProp.Value.GetString() ?? "";
                        var actualCommand = actualProp.GetString() ?? "";

                        if (!AreCommandsEquivalent(expectedCommand, actualCommand))
                            return false;
                    }
                    // Skip comparison for "columnsCsv" field
                    else if (expectedProp.Name.Equals("columnsCsv", StringComparison.OrdinalIgnoreCase))
                    {
                        // Skip this field entirely - don't compare
                        continue;
                    }
                    else
                    {
                        // For other properties, do smart comparison (treating null and empty strings as equal)
                        if (!AreJsonElementsEquivalent(expectedProp.Value, actualProp))
                            return false;
                    }
                }

                // Check if actual has any extra properties not in expected
                foreach (var actualProp in actualRoot.EnumerateObject())
                {
                    // Skip columnsCsv field in this check too
                    if (actualProp.Name.Equals("columnsCsv", StringComparison.OrdinalIgnoreCase))
                        continue;

                    if (!expectedRoot.TryGetProperty(actualProp.Name, out _))
                        return false;
                }

                return true;
            }

            return false;
        }
        catch
        {
            // If JSON parsing fails, fall back to exact comparison
            return false;
        }
    }

    /// <summary>
    /// Compares two JSON elements with smart handling for null and empty strings
    /// </summary>
    private static bool AreJsonElementsEquivalent(JsonElement expected, JsonElement actual)
    {
        // Handle the case where both are strings
        if (expected.ValueKind == JsonValueKind.String && actual.ValueKind == JsonValueKind.String)
        {
            var expectedStr = expected.GetString() ?? "";
            var actualStr = actual.GetString() ?? "";

            // Treat null and empty string as equivalent
            if (string.IsNullOrEmpty(expectedStr) && string.IsNullOrEmpty(actualStr))
                return true;

            return expectedStr == actualStr;
        }

        // Handle the case where one is null and the other is an empty string
        if ((expected.ValueKind == JsonValueKind.Null && actual.ValueKind == JsonValueKind.String) ||
            (expected.ValueKind == JsonValueKind.String && actual.ValueKind == JsonValueKind.Null))
        {
            var stringValue = expected.ValueKind == JsonValueKind.String ?
                expected.GetString() ?? "" :
                actual.GetString() ?? "";

            return string.IsNullOrEmpty(stringValue);
        }

        // For all other cases, do exact comparison
        var expectedJson = JsonSerializer.Serialize(expected);
        var actualJson = JsonSerializer.Serialize(actual);
        return expectedJson == actualJson;
    }

    /// <summary>
    /// Compares two command strings by extracting and comparing only the base command (ignoring flags)
    /// </summary>
    public static bool AreCommandsEquivalent(string expectedCommand, string actualCommand)
    {
        if (string.IsNullOrWhiteSpace(expectedCommand) && string.IsNullOrWhiteSpace(actualCommand))
            return true;

        if (string.IsNullOrWhiteSpace(expectedCommand) || string.IsNullOrWhiteSpace(actualCommand))
            return false;

        var expectedBase = ExtractBaseCommand(expectedCommand.Trim());
        var actualBase = ExtractBaseCommand(actualCommand.Trim());

        return expectedBase.Equals(actualBase, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Extracts the base command from a command string (everything before the first flag starting with -)
    /// </summary>
    public static string ExtractBaseCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
            return "";

        var parts = command.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var baseCommandParts = new List<string>();

        foreach (var part in parts)
        {
            // Stop when we encounter a flag (starts with -)
            if (part.StartsWith('-'))
                break;

            baseCommandParts.Add(part);
        }

        return string.Join(" ", baseCommandParts);
    }
}

class MockStreamingService : IStreamingService
{
    private readonly ILogger<MockStreamingService> _logger;

    public MockStreamingService(ILogger<MockStreamingService> logger)
    {
        _logger = logger;
    }

    public Task StreamActionUpdateAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Streaming message for action {ThreadId}: {Message}",
            threadId, message);
        return Task.CompletedTask;
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

    public Task StreamTaskUpdateAsync(Guid threadId, string taskData, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Task update for thread {ThreadId}: {TaskData}", threadId, taskData);
        return Task.CompletedTask;
    }

    public Task StreamIncidentUpdateAsync(Guid threadId, string incidentData, Guid? messageId = null, DateTime? recordedDateTime = null, StreamMessageType? messageType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Incident update for thread {ThreadId} with type {Type}: {Message}",
            threadId, messageType, incidentData);
        return Task.CompletedTask;
    }
}
