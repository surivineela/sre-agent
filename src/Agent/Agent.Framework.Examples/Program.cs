// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Reflection;
using System.Text.Json;
using Agent.Core.Attributes;
using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Data.DataModels;
using Agent.Framework;
using Agent.Framework.Skills;
using Agent.Graph.Crawler.Metrics;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Agent.Prometheus.Services;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.Helpers;
using Agent.Runtime.Interfaces;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.TeamsChatServices;
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
    [AgentTool(ToolMode.Auto)]
    public string GetData() => "Test Data";

    [RequiresApproval]
    public string GetDataWithApproval() => "Test Data";
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

        Instructions = new PromptText($"You are {Name}, you can delegate to agent2 to get data").WithHandoffInstructions();
    }
}

class Agent2 : Agent<CustomContext>
{
    public Agent2(
        TestService testService // can be injected with DI
    ) : base("Agent2")
    {
        Tools = [
            AIFunctionFactory.Create(testService.GetData)
        ];

        HandoffDescription = "Handoff to get data";

        Instructions = new PromptText($"You are {Name}, use the tool to get data").WithHandoffInstructions();
    }
}

class MockApprovalRepository
{
    private Dictionary<string, Approval> _approvals = new Dictionary<string, Approval>();

    public Task<Approval?> GetApprovalAsync(Guid threadId, string title)
    {
        return Task.FromResult<Approval?>(_approvals[threadId.ToString() + title]);
    }

    public Task CreateApprovalAsync(Approval approval)
    {
        _approvals[approval.ThreadId + approval.Title] = approval;
        return Task.CompletedTask;
    }
}

class MockStreamingService : IStreamingService
{
    private readonly ILogger<MockStreamingService> _logger;

    public MockStreamingService(ILogger<MockStreamingService> logger)
    {
        _logger = logger;
    }

    public Task StreamMessageAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Streaming message for thread {ThreadId} with type {Type}: {Message}",
            threadId, type, message);
        return Task.CompletedTask;
    }

    public Task StreamChatResponseUpdateAsync(Guid threadId, ChatResponseUpdate update, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Streaming message for thread {ThreadId}", threadId);
        return Task.CompletedTask;
    }

    public Task StreamThreadUpdateAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Thread update for thread {ThreadId} with type {Type}: {Message}",
            threadId, type, message);
        return Task.CompletedTask;
    }

    public Task StreamActionUpdateAsync(Guid threadId, string message, StreamMessageType? type, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Streaming action for thread {ThreadId}", threadId);
        return Task.CompletedTask;
    }

    public Task StreamTaskUpdateAsync(Guid threadId, string taskData, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Task update for thread {ThreadId}: {TaskData}", threadId, taskData);
        return Task.CompletedTask;
    }

    public Task StreamIncidentUpdateAsync(Guid threadId, string incidentData, Guid? messageId = null, DateTime? recordedDateTime = null, StreamMessageType? messageType = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Streaming incident for thread {ThreadId}", threadId);
        return Task.CompletedTask;
    }

    public Task StreamTodoPlanUpdateAsync(Guid threadId, string todoPlanData, Guid? messageId = null, DateTime? recordedDateTime = null, CancellationToken cancellationToken = default)
    {
        _logger.LogInternalInformation("Mock: Todo plan update for thread {ThreadId}: {TodoPlanData}", threadId, todoPlanData);
        return Task.CompletedTask;
    }
}

class Program
{
    static void Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(
            new HostApplicationBuilderSettings
            {
                EnvironmentName = Environments.Development
            });

        builder.Services.AddLogging(loggingBuilder =>
        {
            loggingBuilder.ClearProviders();
            loggingBuilder.AddConsole();
            loggingBuilder.AddDebug();
        });

        var agentModeString = builder.Configuration.GetSection("AppSettings:Core:Azure:Action:Mode").Get<string>();

        // register the specific IAgentModeConfigurator implementation
        if (string.Equals(agentModeString, "ReadOnly", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IAgentModeConfigurator<CustomContext>, ReadOnlyAgentModeConfigurator<CustomContext>>();
        }
        else if (string.Equals(agentModeString, "Autonomous", StringComparison.OrdinalIgnoreCase))
        {
            builder.Services.AddSingleton<IAgentModeConfigurator<AgentContext>, AutonomousAgentModeConfigurator<AgentContext>>();
        }
        else
        {
            builder.Services.AddSingleton<IAgentModeConfigurator<CustomContext>, DefaultAgentModeConfigurator<CustomContext>>();
        }

        builder.LoadAppSettings(builder.Environment.IsDevelopment());
        builder.ValidateAndRegisterAppSettings<AppSettings>();
        builder.Services
            .AddSingleton<IGraphDatabaseClient, GremlinGraphDatabaseClient>()
            .AddTransient<IGraphDBPlugin, GraphDBPlugin>()
            .AddSingleton<SinkService>()
            .AddSingleton<ThreadService>()
            .AddSingleton<IStreamingService>(sp =>
            {
                var logger = sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger<MockStreamingService>();
                return new MockStreamingService(logger);
            })
            .AddSingleton<IAgentOutboundCommunicationService, OutboundCommunicationService>()
            .AddSingleton<IPostToTeamsPlugin, PostToTeamsPlugin>()
            .AddSingleton<IArmPlugin, ArmPlugin>()
            .AddSingleton<IContainerAppPlugin, ContainerAppPlugin>()
            .AddSingleton<IPrometheusQueryService, PrometheusQueryService>()
            .AddSingleton<IAzureMetricsClient, AzureMetricsClient>()
            .AddSingleton<IKubernetesClientFactory, KubernetesClientFactory>()
            .AddSingleton<IKubePlugin, KubePlugin>()
            .AddTransient<GraphDBPluginDefinition>()
            .AddTransient<ContainerAppPluginDefinition>()
            .AddTransient<KubePluginDefinition>()
            .AddTransient<ArmPluginDefinition>()
            .AddSingleton<ArmHelper>();

        builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
        builder.Services.AddArmHelperHttpClient();
        builder.Services
            .AddHttpClient()
            .AddCosmosClient();

        builder.Services.AddSingleton<ILogAnalyticsService, LogAnalyticsService>();
        builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
        builder.Services.AddSingleton<IArmClientFactory, ArmClientFactory>();
        builder.Services.AddSingleton<IYamlToolFunctionFactory<CustomContext>, YamlToolFunctionFactory<CustomContext>>();
        builder.Services.AddSingleton<IToolFactory<CustomContext>, ToolFactory<CustomContext>>(sp =>
        {
            return new ToolFactory<CustomContext>(
                logger: sp.GetRequiredService<ILogger<ToolFactory<CustomContext>>>(),
                serviceProvider: sp,
                assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.") == true),
                mcpToolsRepository: sp.GetRequiredService<IMcpConnectable>(),
                extensibilityLoader: sp.GetRequiredService<IExtensibilityLoader>(),
                skillRegistry: new EmptySkillRegistry(),
                yamlToolFunctionFactory: sp.GetRequiredService<IYamlToolFunctionFactory<CustomContext>>());
        });

        builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();

        builder.Services.AddSingleton<IAgentFactory<CustomContext>>(sp =>
        {
            var logger = sp.GetRequiredService<ILogger<AgentFactory<CustomContext>>>();
            var toolsRepository = sp.GetRequiredService<IToolFactory<CustomContext>>();
            var chatClientProvider = sp.GetRequiredService<ChatClientProvider>();
            var modeConfigurator = sp.GetRequiredService<IAgentModeConfigurator<CustomContext>>();
            return new AgentFactory<CustomContext>(
                logger: logger,
                toolFactory: toolsRepository,
                chatClientProvider: chatClientProvider,
                assembliesToScan: [],
                modeConfigurator: modeConfigurator,
                agentsYamlDirectory: Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agents"),
                commonToolsYamlDirectory: null);
        });

        // Configure chat services
        builder.Services
            .ConfigureIChatCompletionService()
            .ConfigureAzureOpenAIClient()
            .ConfigureIChatClient(builder.Configuration)
            .ConfigureIEmbeddingGenerator(builder.Configuration);

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
        await host.StartAsync();
        var graphDBPlugin = host.Services.GetRequiredService<IGraphDBPlugin>();
        // var graphDBDefinition = new GraphDBPluginDefinition(graphDBPlugin);
        var containerAppPlugin = host.Services.GetRequiredService<IContainerAppPlugin>();
        // var containerAppDefinition = new ContainerAppPluginDefinition(containerAppPlugin);
        var kubePlugin = host.Services.GetRequiredService<IKubePlugin>();
        // var kubePluginDefinition = new KubePluginDefinition(kubePlugin);

        var chatClient = host.Services.GetRequiredService<IChatClient>();
        var agentFactory = host.Services.GetRequiredService<IAgentFactory<CustomContext>>();

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
                    SkillRegistry = new EmptySkillRegistry(),
                    AmbientContextProvider = DisabledAmbientContextProvider.Instance
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
        logger.LogInternalInformation("Starting basic example with DI");

        var output = await Runner.RunAsync(
            startingAgent: agent1,
            input: [new ChatMessage(ChatRole.User, "Get me some data")],
            config: new RunConfig
            {
                ChatClient = chatClient,
                LoggerFactory = loggerFactory,
                SkillRegistry = new EmptySkillRegistry(),
                AmbientContextProvider = DisabledAmbientContextProvider.Instance
            },
            context: new CustomContext()
        );

        foreach (var message in output.Input)
        {
            logger.LogInternalInformation(JsonSerializer.Serialize(message));
        }

        foreach (var message in output.NewItems)
        {
            logger.LogInternalInformation(JsonSerializer.Serialize(message));
        }

        logger.LogInternalInformation($"Final Output: {output.Output}");
    }

    static async Task RunBasicExampleNoDIAsync(HostApplicationBuilder builder)
    {
        using var host = builder.Build();
        var config = host.Services.GetRequiredService<IConfiguration>();

        var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();

        // construct agents manually from framework types
        var agent1 = new Agent<CustomContext>("Agent1");
        agent1.Instructions = new PromptText($"You are {agent1.Name}, you can delegate to agent2 to get data").WithHandoffInstructions();

        var agent2 = new Agent<CustomContext>("Agent2");
        agent2.Instructions = new PromptText($"You are {agent2.Name}, use the tool to get data").WithHandoffInstructions();
        agent2.HandoffDescription = "Handoff to get data";

        var testService = new TestService();

        agent2.Tools = [
            AIFunctionFactory.Create(testService.GetData)
        ];

        agent1.Handoffs = [
            Handoff<CustomContext>.Create(agent: agent2)
        ];

        var chatClient = host.Services.GetRequiredService<IChatClient>();

        var logger = loggerFactory.CreateLogger("Agent.Framework.Examples.RunBasicExampleNoDIAsync");
        logger.LogInternalInformation("Starting basic example without DI");

        var output = await Runner.RunAsync(
            startingAgent: agent1,
            input: [new ChatMessage(ChatRole.User, "Get me some data")],
            config: new RunConfig
            {
                ChatClient = chatClient,
                LoggerFactory = loggerFactory,
                SkillRegistry = new EmptySkillRegistry(),
                AmbientContextProvider = DisabledAmbientContextProvider.Instance
            },
            context: new CustomContext()
        );

        foreach (var message in output.Input)
        {
            logger.LogInternalInformation(JsonSerializer.Serialize(message));
        }

        foreach (var message in output.NewItems)
        {
            logger.LogInternalInformation(JsonSerializer.Serialize(message));
        }

        logger.LogInternalInformation($"Final Output: {output.Output}");
    }

    public static async Task ReasoningLoop(HostApplicationBuilder builder)
    {
        // Initialize the Agents
        using var host = builder.Build();
        var config = host.Services.GetRequiredService<IConfiguration>();

        var loggerFactory = host.Services.GetRequiredService<ILoggerFactory>();

        // construct agents manually from framework types
        var agent1 = new Agent<CustomContext>("Agent1");
        agent1.Instructions = new PromptText($"You are {agent1.Name}, you can delegate to agent2 to get data").WithHandoffInstructions();

        var agent2 = new Agent<CustomContext>("Agent2");
        agent2.Instructions = new PromptText($"You are {agent2.Name}, use the tool to get data").WithHandoffInstructions();
        agent2.HandoffDescription = "Handoff to get data";

        var testService = new TestService();

        agent2.Tools = [
            AIFunctionFactory.Create(testService.GetDataWithApproval)
        ];

        agent1.Handoffs = [
            Handoff<CustomContext>.Create(agent: agent2)
        ];

        var chatClient = host.Services.GetRequiredService<IChatClient>();

        var context = new CustomContext();
        string userInput = "Get me some data";
        var chatHistory = new List<ChatMessage> { new(ChatRole.User, userInput) };

        // The reasoning loop starts here
        while (true)
        {
            var output = await Runner.RunAsync(
                startingAgent: agent1,
                input: chatHistory,
                config: new RunConfig
                {
                    ChatClient = chatClient,
                    LoggerFactory = loggerFactory,
                    SkillRegistry = new EmptySkillRegistry(),
                    AmbientContextProvider = DisabledAmbientContextProvider.Instance
                },
                context: context
            );
            chatHistory.AddRange(output.NewItems);

            // Check if there are any manual tool calls (Approval)
            if (output.ManualToolCalls != null && output.ManualToolCalls.Count > 0)
            {
                foreach (var toolCall in output.ManualToolCalls)
                {
                    var checkResult = await CheckApproval(context, toolCall, new MockApprovalRepository(), loggerFactory.CreateLogger("CheckApprovalActivity"));
                    if (checkResult.ApprovalStatus == ToolApprovalStatus.NotRequired || checkResult.ApprovalStatus == ToolApprovalStatus.Approved)
                    {
                        var functionResult = await toolCall.Tool!.InvokeAsync(new AIFunctionArguments(toolCall.FunctionCall.Arguments));
                        var result = new FunctionResultContent(toolCall.FunctionCall.CallId, functionResult);
                        chatHistory.Add(new ChatMessage(ChatRole.Tool, [result]));
                    }
                    else if (checkResult.ApprovalStatus == ToolApprovalStatus.Pending)
                    {
                        // Generate approval link
                        var link = $"https://approval-system.example.com/approve?approvalId={checkResult.ApprovalId}";
                        chatHistory.RemoveAt(chatHistory.Count - 1); // Remove the function call message
                        chatHistory.Add(new ChatMessage(ChatRole.Assistant, "Approval required: " + link));
                    }
                    else
                    {
                        chatHistory.RemoveAt(chatHistory.Count - 1); // Remove the function call message
                        chatHistory.Add(new ChatMessage(ChatRole.Assistant, "The approval request of this action got denied."));
                    }
                }
            }
            else
            {
                break; // Exit the loop if there are no manual tool calls
            }
        }
    }

    public static async Task<CheckApprovalActivityOutput> CheckApproval(CustomContext context, ManualToolCall toolCall, MockApprovalRepository approvalRepo, ILogger logger)
    {
        try
        {
            if (toolCall.Tool == null)
            {
                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.NotRequired,
                };
            }

            // Check if requiers approval
            var attribute = toolCall.Tool.UnderlyingMethod?.GetCustomAttribute<RequiresApprovalAttribute>();
            if (attribute == null)
            {
                return new CheckApprovalActivityOutput()
                {
                    ApprovalStatus = ToolApprovalStatus.NotRequired,
                };
            }

            var approvalTitle = ApprovalHelper.GenerateUniqueApprovalTitle(
                context.ThreadId.ToString(),
                "instance-id",
                toolCall.FunctionCall.Name,
                toolCall.FunctionCall.Arguments ?? new Dictionary<string, object?>());

            var approval = await approvalRepo.GetApprovalAsync(context.ThreadId, approvalTitle);

            if (approval == null ||
                (approval.Status == ApprovalDecision.Approved && string.IsNullOrEmpty(approval.OboToken) && attribute != null))
            {
                var description = attribute.DisplayMessage ?? string.Empty;

                // Create a new approval document
                var newApproval = new Approval(
                    Id: Guid.NewGuid(),
                    ThreadId: context.ThreadId.ToString(),
                    Title: approvalTitle,
                    Description: description,
                    Status: ApprovalDecision.Pending,
                    CreatedTimestamp: DateTime.UtcNow,
                    DecisionTimestamp: null,
                    OrchestrationId: null,
                    AgentContextId: null,
                    DecisionUser: null,
                    OboToken: null,
                    OboTokenScope: null);

                await approvalRepo.CreateApprovalAsync(newApproval);

                logger.LogInternalInformation("Created new approval document: {ApprovalId}, threadId: {ThreadId}, title: {Title}, status ToolApprovalStatus.Pending", newApproval.Id, context.ThreadId, newApproval.Title);

                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = newApproval.Id,
                    ApprovalStatus = ToolApprovalStatus.Pending,
                };
            }
            else
            {
                logger.LogInternalInformation("Found existing approval document: {ApprovalId}, threadId: {ThreadId}, title: {Title}, status {Status}", approval.Id, context.ThreadId, approval.Title, approval.Status);
                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = approval.Id,
                    ApprovalStatus = ApprovalDocument.ToToolApprovalStatus(approval.Status),
                };
            }
        }
        catch (Exception ex)
        {
            logger.LogError("Error while checking approval: {Message}", ex.Message);
            return new CheckApprovalActivityOutput()
            {
                ApprovalStatus = ToolApprovalStatus.Pending,
            };
        }
    }
}
