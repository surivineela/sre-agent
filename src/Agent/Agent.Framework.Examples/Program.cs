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
using Agent.Graph.Crawler.Metrics;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Prometheus.Services;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.Helpers;
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
    [RequiresApproval]
    public string GetData() => "Test Data";
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
            .AddTransient<GraphDBPluginDefinition>()
            .AddTransient<ContainerAppPluginDefinition>()
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
        builder.Services.AddSingleton<IToolFactory, ToolFactory>();

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

        var toolsRepository = host.Services.GetRequiredService<IToolFactory>();

        var agentFactory = new AgentFactory<CustomContext>(
            logger: host.Services.GetRequiredService<ILogger<AgentFactory<CustomContext>>>(),
            toolsRepository: toolsRepository
        );

        // Load agents from YAML files in the agents folder
        var agentsFolder = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "agents");
        if (Directory.Exists(agentsFolder))
        {
            var yamlFiles = Directory.GetFiles(agentsFolder, "*.yaml", SearchOption.AllDirectories)
                           .Concat(Directory.GetFiles(agentsFolder, "*.yml", SearchOption.AllDirectories));

            foreach (var yamlFile in yamlFiles)
            {
                try
                {
                    agentFactory.LoadAgentFromFile(yamlFile);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load agent from {yamlFile}: {ex.Message}");
                }
            }
        }
        else
        {
            Console.WriteLine($"Agents folder not found at: {agentsFolder}");
        }

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

    public static async Task ReasoningLoop(HostApplicationBuilder builder)
    {
        // Initialize the Agents
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

        agent2.ManualTools = [
            AIFunctionFactory.Create(testService.GetData)
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
                    LoggerFactory = loggerFactory
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
                        var functionResult = await toolCall.Tool!.InvokeAsync(toolCall.FunctionCall.Arguments);
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
                (approval.Status == ApprovalDecision.Approved && string.IsNullOrEmpty(approval.OboToken) && attribute != null && attribute.UseOboToken))
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
                    OboToken: null);

                await approvalRepo.CreateApprovalAsync(newApproval);

                logger.LogInformation("Created new approval document: {ApprovalId}, threadId: {ThreadId}, title: {Title}, status ToolApprovalStatus.Pending", newApproval.Id, context.ThreadId, newApproval.Title);

                return new CheckApprovalActivityOutput()
                {
                    ApprovalId = newApproval.Id,
                    ApprovalStatus = ToolApprovalStatus.Pending,
                };
            }
            else
            {
                logger.LogInformation("Found existing approval document: {ApprovalId}, threadId: {ThreadId}, title: {Title}, status {Status}", approval.Id, context.ThreadId, approval.Title, approval.Status);
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
