using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.ChatCompletion;
using Agent.Core.Configuration;
using Agent.Core.Configuration;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Agent.Core;
using Agent.Plugins;
using Microsoft.Extensions.AI;
using Azure.AI.OpenAI;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Logging;
using System.ComponentModel;
using Gremlin.Net.Driver;
using Agent.Core.Helpers;
using System.Text.Json;
using OpenAI.Chat;
using Agent.Runtime.SubAgents;

namespace Agent.Runtime
{
    public static class AgentsConfigurationExtensions
    {
        public static void ConfigureAgents(this IServiceCollection services)
        {
            _ = services
                .ConfigureIChatCompletionService()
                .ConfigureAzureOpenAIClient()
                .ConfigureIChatClient()
                .AddTransient<Kernel>(sp =>
                {
                    return new Kernel(sp);
                })
                .AddSingleton<MetaAgentPlugin>()
                .AddSingleton<ISubscriptionPlugin, SubscriptionPlugin>()
                .AddSingleton<SubscriptionPluginDefinition>()
                .AddSingleton<IGraphDatabaseManager, GremlinGraphDatabaseManager>()
                .AddSingleton<IGraphDBPlugin, GraphDBPlugin>()
                .AddSingleton<GraphDBPluginDefinition>()
                .AddSingleton<ITimePlugin, TimePlugin>()
                .AddSingleton<TimePluginDefinition>()
                .AddSingleton<GraphDBQueryAgent>()
                .AddSingleton<ArchitectureAgent>()
                .AddSingleton<GenericAgent>()
                // Agent is defined by its name, instructions, and the plugins it uses
                // In future we load the agent and conversation from a data store. For now it is all in memory
                .AddSingleton(s =>
                {
                    var agent = new Agent(
                        "main",
                        //IssueFinderAgent.SystemMessage,
                        @"You are SRE Agent. You must delegate to other agents",
                        s.GetRequiredService<Kernel>(),
                        s.GetRequiredService<IOptions<AzureSettings>>());

                    //agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<SubscriptionPluginDefinition>(), "SubscriptionPlugin");
                    agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<MetaAgentPlugin>(), "MetaAgentPlugin");

                    return agent;
                })
                .AddSingleton<Session>(s =>
                {
                    var conversation = new Session();
                    conversation.AddAgent(s.GetRequiredService<Agent>());
                    return conversation;
                });
        }

        public static IServiceCollection ConfigureIChatCompletionService(this IServiceCollection services)
        {
            return services
                .AddSingleton<IChatCompletionService>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var azureSettings = config.GetSection("Azure").Get<AzureSettings>();
                    if (azureSettings == null)
                    {
                        throw new NullReferenceException("Azure settings are required.");
                    }
                    return new AzureOpenAIChatCompletionService(
                        deploymentName: azureSettings.OpenAI.DeploymentName,
                        endpoint: azureSettings.OpenAI.Endpoint,
                        apiKey: azureSettings.OpenAI.ApiKey
                    );
                });
        }

        public static IServiceCollection ConfigureAzureOpenAIClient(this IServiceCollection services)
        {
            return services
                .AddSingleton<AzureOpenAIClient>(sp =>
                {
                    var config = sp.GetRequiredService<IConfiguration>();
                    var azureSettings = config.GetSection("Azure").Get<AzureSettings>();
                    if (azureSettings == null)
                    {
                        throw new NullReferenceException("Azure settings are required.");
                    }

                    return new AzureOpenAIClient(
                        endpoint: new Uri(azureSettings.OpenAI.Endpoint),
                        credential: new System.ClientModel.ApiKeyCredential(azureSettings.OpenAI.ApiKey)
                    );
                });
        }

        public static IServiceCollection ConfigureIChatClient(this IServiceCollection services)
        {
            return services
                .AddSingleton<IChatClient>(sp =>
                {
                    var client = sp.GetRequiredService<AzureOpenAIClient>();
                    var config = sp.GetRequiredService<IConfiguration>();
                    var azureSettings = config.GetSection("Azure").Get<AzureSettings>();
                    if (azureSettings == null)
                    {
                        throw new NullReferenceException("Azure settings are required.");
                    }

                    return new ChatClientBuilder(client.AsChatClient(azureSettings.OpenAI.DeploymentName)).Build();
                });
        }
    }

    public class MetaAgentPlugin
    {
        ILogger<MetaAgentPlugin> _logger;
        IChatClient _chatClient;
        ArchitectureAgent _badArchitectureAgent;
        GenericAgent _genericAgent;

        public MetaAgentPlugin(IChatClient chatClient, ILogger<MetaAgentPlugin> logger, ArchitectureAgent badArchitectureAgent, GenericAgent genericAgent)
        {
            _chatClient = chatClient;
            _logger = logger;
            _badArchitectureAgent = badArchitectureAgent;
            _genericAgent = genericAgent;
        }

        [KernelFunction("launch_architecture_agent")]
        [Description("This agent will answer quetions relating to the architecture of a service.")]
        public async Task<string> LaunchBadArchitectureAgentAsync(string question)
        {
            _logger.LogInformation("Invoking architecture agent");
            string answer = await _badArchitectureAgent.Ask(question);
            _logger.LogInformation($"Architecture agent responded with: {answer}");
            return answer;
        }

        [KernelFunction("launch_generic_agent")]
        [Description("If you can't find a better agent, try this agent")]
        public async Task<string> LaunchGenericAgentAsync(string question)
        {
            _logger.LogInformation("Invoking generic agent");
            string answer = await _genericAgent.Ask(question);
            _logger.LogInformation($"Generic agent responded with: {answer}");
            return answer;
        }
    }
}
