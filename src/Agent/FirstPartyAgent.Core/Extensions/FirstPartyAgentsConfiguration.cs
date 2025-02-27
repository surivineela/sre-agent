using Agent.Core.Configuration;
using Agent.Plugins;
using Agent.Runtime;
using FirstPartyAgent.Agents;
using FirstPartyAgent.Models;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;

namespace FirstPartyAgent.Runtime
{
    public static class FirstPartyAgentsConfigurationExtensions
    {
        public static void ConfigureAgents(this IServiceCollection services, AgentMode agentMode)
        {
            _ = services
                .ConfigureIChatCompletionService()
                .AddTransient<Kernel>(sp =>
                {
                    return new Kernel(sp);
                })

                // Agent is defined by its name, instructions, and the plugins it uses
                // In future we load the agent and conversation from a data store. For now it is all in memory
                .AddSingleton(s =>
                {
                    string systemMessage = ICMAgent.SystemMessage;
                    switch (agentMode)
                    {
                        case AgentMode.ICM:
                            systemMessage = ICMAgent.SystemMessage;
                            break;
                        case AgentMode.GithubIssueTagger:
                            systemMessage = GithubIssueTaggerAgent.SystemMessage;
                            break;
                        case AgentMode.ACA:
                            break;
                        default:
                            break;
                    }
                    var agent = new Agent.Runtime.Agent(
                        "main",
                        systemMessage,
                        s.GetRequiredService<Kernel>(),
                        s.GetRequiredService<IOptions<AzureSettings>>(),
                        s.GetRequiredService<Microsoft.Extensions.AI.IChatClient>(),
                        s.GetRequiredService<ILoggerFactory>().CreateLogger<Agent.Runtime.Agent>());

                    switch (agentMode)
                    {
                        case AgentMode.ICM:
                            agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<IKustoPlugin>(), "KustoPlugin");
                            agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<ICMPlugin>(), "IcmPlugin");
                            break;
                        case AgentMode.GithubIssueTagger:
                            agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<GitHubIssuePluginDefinition>(), "GitHubIssuePlugin");
                            agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<AzureSearchPluginDefinition>(), "AzureSearchPlugin");
                            break;
                        case AgentMode.ACA:
                            break;
                        default:
                            break;
                    }

                    return agent;
                })
                .AddSingleton<Session>(s =>
                {
                    var conversation = new Session(s.GetRequiredService<ILoggerFactory>().CreateLogger<Session>());
                    conversation.AddAgent(s.GetRequiredService<Agent.Runtime.Agent>());
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

        public static IServiceCollection ConfigureSemanticKernel(this IServiceCollection services, AgentMode agentMode)
        {
            // Configure Semantic Kernel
            services.AddScoped<Kernel>(sp =>
            {
                var config = sp.GetRequiredService<IConfiguration>();

                var azureSettings = config.GetSection("Azure").Get<AzureSettings>();

                if (azureSettings == null)
                {
                    throw new NullReferenceException("Azure settings are required.");
                }

                var kernelBuilder = Kernel.CreateBuilder();
                kernelBuilder.AddAzureOpenAIChatCompletion(
                   deploymentName: azureSettings.OpenAI.DeploymentName,
                   endpoint: azureSettings.OpenAI.Endpoint,
                   apiKey: azureSettings.OpenAI.ApiKey);


                kernelBuilder.Services.AddLogging(builder =>
                {
                    // Use configuration for logging levels
                    builder.AddConfiguration(config.GetSection("Logging"));
                    builder.AddConsole();
                });

                switch (agentMode)
                {
                    case AgentMode.ICM:
                        kernelBuilder.Plugins.AddFromObject(sp.GetRequiredService<ICMPlugin>(), "IcmPlugin");
                        kernelBuilder.Plugins.AddFromObject(sp.GetRequiredService<IKustoPlugin>(), "KustoPlugin");
                        break;
                    case AgentMode.GithubIssueTagger:
                        kernelBuilder.Plugins.AddFromObject(sp.GetRequiredService<GitHubIssuePluginDefinition>(), "GitHubIssuePlugin");
                        kernelBuilder.Plugins.AddFromObject(sp.GetRequiredService<AzureSearchPluginDefinition>(), "AzureSearchPlugin");
                        break;
                    case AgentMode.ACA:
                        break;
                    default:
                        break;
                }

                return kernelBuilder.Build();
            });

            return services;
        }
    }
}
