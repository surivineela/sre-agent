// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.Services;
using Agent.Runtime.TeamsChatServices;
using Agent.Prometheus.Services;
using Agent.Graph.Crawler.Metrics;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.CommandLineUtils;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Framework.Examples;

class CustomContext
{
    public Guid ThreadId { get; set; } = Guid.NewGuid();
}

class TestService
{
    public string GetData() => "Test Data";
}

class ToolsRepository : IToolsRepository
{
    private readonly GraphDBPluginDefinition _graphDBPluginDefinition;
    private readonly ContainerAppPluginDefinition _containerAppPluginDefinition;

    private readonly KubePluginDefinition _kubePluginDefinition;

    public ToolsRepository(GraphDBPluginDefinition graphDBPluginDefinition, ContainerAppPluginDefinition containerAppPluginDefinition, KubePluginDefinition kubePluginDefinition)
    {
        _graphDBPluginDefinition = graphDBPluginDefinition;
        _containerAppPluginDefinition = containerAppPluginDefinition;
        _kubePluginDefinition = kubePluginDefinition;
    }


    public AIFunction FindAiFunction(string name)
    {
        if (name == "get_resource_count")
        {
            return AIFunctionFactory.Create(_graphDBPluginDefinition.GetResourceCount);
        }
        if (name == "list_subscriptions")
        {
            return AIFunctionFactory.Create(_graphDBPluginDefinition.ListSubscriptions);
        }
        if (name == "get_managed_resources_info")
        {
            return AIFunctionFactory.Create(_graphDBPluginDefinition.GetManagedResourcesInfoAsync);
        }
        if (name == "discover_applications")
        {
            return AIFunctionFactory.Create(_graphDBPluginDefinition.DiscoverApplications);
        }
        if (name == "list_resources_by_type")
        {
            return AIFunctionFactory.Create(_graphDBPluginDefinition.ListResourcesByType);
        }
        if (name == "list_resource_groups")
        {
            return AIFunctionFactory.Create(_graphDBPluginDefinition.ListResourceGroups);
        }
        if (name == "get_container_app_info")
        {
            return AIFunctionFactory.Create(_containerAppPluginDefinition.GetContainerAppInfoAsync);
        }
        if (name == "list_revisions")
        {
            return AIFunctionFactory.Create(_containerAppPluginDefinition.ListRevisionsAsync);
        }
        if (name == "list_container_apps")
        {
            return AIFunctionFactory.Create(_containerAppPluginDefinition.ListContainerAppsAsync);
        }
        if (name == "kubectl_read_command")
        {
            return AIFunctionFactory.Create(_kubePluginDefinition.RunKubectlReadCommandAsync);
        }
        if (name == "kubectl_write_command")
        {
            return AIFunctionFactory.Create(_kubePluginDefinition.RunKubectlWriteCommandAsync);
        }
        if (name == "check_apiserver_status")
        {
            return AIFunctionFactory.Create(_kubePluginDefinition.GetAPIServerStatusAsync);
        }

        throw new NotImplementedException($"Tool {name} not found");
    }
}

class Agent1 : Agent<CustomContext>
{
    public Agent1(
        Agent2 agent2 // can be injected with DI
    ) : base("Agent1")
    {
        Handoffs = [
            Handoff<CustomContext>.Create(agent: agent2)
        ];

        Instructions = Prompt.PromptWithHandoffInstructions($"You are {Name}, you can delegate to agent2 to get data");
    }
}

class Agent2 : Agent<CustomContext>
{
    public Agent2(
        TestService testService // can be injected with DI
    ) : base("Agent2")
    {
        AutoTools = [
            AIFunctionFactory.Create(testService.GetData)
        ];

        HandoffDescription = "Handoff to get data";

        Instructions = Prompt.PromptWithHandoffInstructions($"You are {Name}, use the tool to get data");
    }
}

class Program
{
    static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });

        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddConsole();
        });

        builder.LoadAppSettings(builder.Environment.IsDevelopment());
        builder.ValidateAndRegisterAppSettings<AppSettings>();
        builder.Services
            .AddSingleton<IGraphDatabaseClient, GremlinGraphDatabaseClient>()
            .AddTransient<IGraphDBPlugin, GraphDBPlugin>()
            .AddSingleton<IThreadOrchestrationManager, CosmosThreadOrchestrationManager>()
            .AddSingleton<SinkService>()
            .AddSingleton<ThreadService>()
            // .AddSingleton<ThreadManagementService>()
            .AddSingleton<IAgentOutboundCommunicationService, OutboundCommunicationService>()
            .AddSingleton<IPostToTeamsPlugin, PostToTeamsPlugin>()
            .AddSingleton<IArmPlugin, ArmPlugin>()
            .AddSingleton<IContainerAppPlugin, ContainerAppPlugin>()
            .AddSingleton<IPrometheusQueryService, PrometheusQueryService>()
            .AddSingleton<IAzureMetricsClient, AzureMetricsClient>()
            .AddSingleton<IKubernetesClientFactory, KubernetesClientFactory>()
            .AddSingleton<IKubePlugin, KubePlugin>()
            .AddSingleton<ArmHelper>();

        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.Services.AddArmHelperHttpClient();
        builder.Services
            .AddLogging(loggingBuilder =>
            {
                loggingBuilder.AddConsole();
                loggingBuilder.AddDebug();
            })
            .AddHttpClient()
            .AddCosmosClient();

        builder.Services.AddSingleton<ILogAnalyticsService, LogAnalyticsService>();
        builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
        builder.Services.AddSingleton<IArmClientFactory, ArmClientFactory>();

        builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();
        // builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
        // builder.Services.AddSingleton<IBot, TeamsBot>()
        //                 .AddSingleton<IBotPollingMessage, TeamsBot>();
        // Add the new polling service

        // builder.Services.AddSingleton<IConfiguration>(builder.Configuration);
        // Configure chat services
        builder.Services.ConfigureIChatCompletionService()
                    .ConfigureAzureOpenAIClient()
                    .ConfigureIChatClient()
                    .ConfigureIEmbeddingGenerator();

        var commandLineApplication = new CommandLineApplication(throwOnUnexpectedArg: true);
        commandLineApplication.HelpOption("-?|-h|--help");

        commandLineApplication.Command("basic-example-di", (command) =>
        {
            command.Description = "Run the basic example using dependency injection";
            command.OnExecute(async () =>
            {
                await RunBasicExampleDIAsync(builder);
                return 0;
            });
        });

        commandLineApplication.Command("basic-example-no-di", (command) =>
        {
            command.Description = "Run the basic example without dependency injection";
            command.OnExecute(async () =>
            {
                await RunBasicExampleNoDIAsync(builder);
                return 0;
            });
        });

        commandLineApplication.Command("agent-factory-example", (command) =>
        {
            command.Description = "Run the agent factory example";
            command.OnExecute(async () =>
            {
                await RunAgentFactoryExampleAsync(builder);
                return 0;
            });
        });

        commandLineApplication.OnExecute(() =>
        {
            commandLineApplication.ShowHelp();
            return 0;
        });

        commandLineApplication.Execute(args);
    }

    static async Task RunAgentFactoryExampleAsync(HostApplicationBuilder builder)
    {
        var host = builder.Build();
        var graphDBPlugin = host.Services.GetRequiredService<IGraphDBPlugin>();
        var graphDBDefinition = new GraphDBPluginDefinition(graphDBPlugin);
        var containerAppPlugin = host.Services.GetRequiredService<IContainerAppPlugin>();
        var containerAppDefinition = new ContainerAppPluginDefinition(containerAppPlugin);
        var kubePlugin = host.Services.GetRequiredService<IKubePlugin>();
        var kubePluginDefinition = new KubePluginDefinition(kubePlugin);

        var chatClient = host.Services.GetRequiredService<IChatClient>();

        var toolsRepository = new ToolsRepository(graphDBDefinition, containerAppDefinition, kubePluginDefinition);

        var agentFactory = new AgentFactory<CustomContext>(
            logger: host.Services.GetRequiredService<ILogger<AgentFactory<CustomContext>>>(),
            toolsRepository: toolsRepository
        );

        var chatHistory = new List<ChatMessage>();
        var lastAgent = agentFactory.GetAgent("meta_agent");
        while (true)
        {
            Console.Write("User>> ");
            var userInput = Console.ReadLine();
            if (string.IsNullOrEmpty(userInput) || userInput.ToLower() == "exit")
            {
                break;
            }
            chatHistory.Add(new ChatMessage(ChatRole.User, userInput));
            var output = await Runner.RunAsync(
                startingAgent: lastAgent,
                input: chatHistory,
                config: new RunConfig
                {
                    ChatClient = chatClient,
                    LoggerFactory = host.Services.GetRequiredService<ILoggerFactory>(),
                },
                context: new CustomContext()
            );

            // foreach (var message in output.Input)
            // {
            //     Console.WriteLine(JsonSerializer.Serialize(message));
            // }

            // foreach (var message in output.NewItems)
            // {
            //     Console.WriteLine(JsonSerializer.Serialize(message));
            // }
            chatHistory.AddRange(output.NewItems);
            lastAgent = output.LastAgent;

            Console.WriteLine($"\n\n{output.LastAgent.Name}: {output.Output}");
        }
    }

    static async Task RunBasicExampleDIAsync(HostApplicationBuilder builder)
    {
        // register agents with DI
        builder.Services.AddSingleton<Agent1>();
        builder.Services.AddSingleton<Agent2>();
        builder.Services.AddSingleton<TestService>();

        using var host = builder.Build();

        var agent1 = host.Services.GetRequiredService<Agent1>();
        var config = host.Services.GetRequiredService<IConfiguration>();
        var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();

        var chatClient = host.Services.GetRequiredService<IChatClient>();

        var logger = loggerFactory.CreateLogger("Agent.Framework.Examples.RunBasicExampleDIAsync");
        logger.LogInformation("Starting basic example with DI");

        var output = await Runner.RunAsync(
            startingAgent: agent1,
            input: [new ChatMessage(ChatRole.User, "Get me some data")],
            config: new RunConfig
            {
                ChatClient = chatClient,
                LoggerFactory = loggerFactory
            },
            context: new CustomContext()
        );

        foreach (var message in output.Input)
        {
            logger.LogInformation(JsonSerializer.Serialize(message));
        }

        foreach (var message in output.NewItems)
        {
            logger.LogInformation(JsonSerializer.Serialize(message));
        }

        logger.LogInformation($"Final Output: {output.Output}");
    }

    static async Task RunBasicExampleNoDIAsync(HostApplicationBuilder builder)
    {
        using var host = builder.Build();
        var config = host.Services.GetRequiredService<IConfiguration>();

        var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();

        // construct agents manually from framework types
        var agent1 = new Agent<CustomContext>("Agent1");
        agent1.Instructions = Prompt.PromptWithHandoffInstructions($"You are {agent1.Name}, you can delegate to agent2 to get data");

        var agent2 = new Agent<CustomContext>("Agent2");
        agent2.Instructions = Prompt.PromptWithHandoffInstructions($"You are {agent2.Name}, use the tool to get data");
        agent2.HandoffDescription = "Handoff to get data";

        var testService = new TestService();

        agent2.AutoTools = [
            AIFunctionFactory.Create(testService.GetData)
        ];

        agent1.Handoffs = [
            Handoff<CustomContext>.Create(agent: agent2)
        ];

        var chatClient = host.Services.GetRequiredService<IChatClient>();

        var logger = loggerFactory.CreateLogger("Agent.Framework.Examples.RunBasicExampleNoDIAsync");
        logger.LogInformation("Starting basic example without DI");

        var output = await Runner.RunAsync(
            startingAgent: agent1,
            input: [new ChatMessage(ChatRole.User, "Get me some data")],
            config: new RunConfig
            {
                ChatClient = chatClient,
                LoggerFactory = loggerFactory
            },
            context: new CustomContext()
        );

        foreach (var message in output.Input)
        {
            logger.LogInformation(JsonSerializer.Serialize(message));
        }

        foreach (var message in output.NewItems)
        {
            logger.LogInformation(JsonSerializer.Serialize(message));
        }

        logger.LogInformation($"Final Output: {output.Output}");
    }
}
