using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Data.Repositories;
using Agent.Framework;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Agent.Tests.Common.Mocks;
using Agent.Tests.Common.Mocks.FunctionCalling;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Moq;

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

            string? apiKey = builder.Configuration["OpenAIKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                throw new InvalidOperationException("OpenAI API key is missing. Pass it as a TestRunParameter.");
            }

            llmDeploymentName = builder.Configuration["OpenAIModel"];
            if (string.IsNullOrEmpty(llmDeploymentName))
            {
                throw new InvalidOperationException("OpenAI API model is missing. Pass it as a TestRunParameter.");
            }

            string? aiEndpoint = builder.Configuration["OpenAIEndpoint"];
            if (string.IsNullOrEmpty(aiEndpoint))
            {
                throw new InvalidOperationException("OpenAI API endpoint is missing. Pass it as a TestRunParameter.");
            }

            builder.Services.AddSingleton(new AzureOpenAIClient(new Uri(aiEndpoint), new System.ClientModel.ApiKeyCredential(apiKey)));
        }
        else
        {
            Console.WriteLine("Eval pipeline is using appsettings. Please make sure you have proper values in appsettings.json.");
            builder.Services.ConfigureAzureOpenAIClient();
        }

        builder.Services.AddLogging(builder =>
        {
            builder.AddConsole();
        });

        builder.Services.AddChatClient(sp => sp.GetRequiredService<AzureOpenAIClient>().GetChatClient(llmDeploymentName).AsIChatClient());

        builder.Services.AddKeyedSingleton<IChatClient>("function-invocation-enabled", (sp, _) =>
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

        builder.Services.AddKeyedSingleton<IChatClient>("helper-agent-reasoning", (sp, _) =>
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
        builder.Services.AddSingleton<IThreadRepository, InmemoryThreadRepository>();
        builder.Services.AddSingleton<IInstanceManagementRepository, InMemoryInstanceManagementRepository>();
        builder.Services.AddSingleton<ThreadService>();
        builder.Services.AddSingleton<SinkService>();
        // NOTE: use mock for teams plugin as we don't rely on teams for Agent Eval.
        builder.Services.AddSingleton(sp => new Mock<IPostToTeamsPlugin>().Object);

        return builder;
    }

    public static HostApplicationBuilder RegisterServicesForAgentFrameworkEval(this HostApplicationBuilder builder, JsonSerializerOptions? toolReplaySerializerOptions = null)
    {
        builder.Services.AddSingleton<ThreadManagementService>();
        builder.Services.AddSingleton<IAgentInboundCommunicationService, InboundCommunicationService>();
        builder.Services.AddSingleton<IAgentOutboundCommunicationService, OutboundCommunicationService>();
        builder.Services.AddTransient<Agent.Runtime.MetaAgent.IAgent, MetaAgent>();
        builder.Services.AddSingleton<IAuthenticationService>(Mock.Of<IAuthenticationService>());
        builder.Services.AddSingleton<ITitleGenerationService, TitleGenerationService>();

        builder.Services.AddSingleton<GraphDBPlugin>();
        builder.Services.AddSingleton<UserInteractionPluginDefinition>();
        builder.Services.AddSingleton<AgentControlFlowPluginDefinition>();

        builder.Services.AddSingleton<IReasoningLoopManager, ReasoningLoopManager>();
        builder.Services.AddSingleton<IReasoningLoopFactory, ReasoningLoopFactory>();
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
            return new AgentFactory<AgentContext>(
                logger: sp.GetRequiredService<ILogger<AgentFactory<AgentContext>>>(),
                toolFactory: sp.GetRequiredService<IToolFactory<AgentContext>>(),
                assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.") == true),
                agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "AgentsV2"),
                commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "CommonPrompts")
            );
        });

        // should be removed later - currently required because ThreadManagementService has code for handling UseAgentFramework=false
        builder.Services.AddSingleton<IAgentsFactory>(sp =>
        {
            return MetaAgentMock.GetMockedThirdPartAgentsFactory(
                graphDBPlugin: sp.GetRequiredService<GraphDBPlugin>()
                );
        });

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

}
