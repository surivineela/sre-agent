using Agent.Core.Configuration;
using Agent.Runtime;
using FirstPartyAgent.Models;
using FirstPartyAgent.Plugins;
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
        public static void ConfigureAgents(this IServiceCollection services, string systemMessage)
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
                    var agent = new Agent.Runtime.Agent(
                        "main",
                        systemMessage,
                        s.GetRequiredService<Kernel>(),
                        s.GetRequiredService<IOptions<AzureSettings>>(),
                        s.GetRequiredService<Microsoft.Extensions.AI.IChatClient>(),
                        s.GetRequiredService<ILoggerFactory>().CreateLogger<Agent.Runtime.Agent>());

                    agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<IKustoPlugin>(), "KustoPlugin");
                    agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<ICMPlugin>(), "IcmPlugin");

                    return agent;
                })
                .AddSingleton<Session>(s =>
                {
                    var conversation = new Session(s.GetRequiredService<ILogger>());
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

        public static IServiceCollection ConfigureSemanticKernel(this IServiceCollection services)
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

                string agentModeStr = config.GetValue("AgentMode", string.Empty);
                var agentMode = Enum.TryParse<AgentMode>(agentModeStr, out var mode) ? mode : AgentMode.ICM;

                if (agentMode == AgentMode.ICM)
                {
                    kernelBuilder.Plugins.AddFromObject(sp.GetRequiredService<ICMPlugin>(), "IcmPlugin");
                    kernelBuilder.Plugins.AddFromObject(sp.GetRequiredService<IKustoPlugin>(), "KustoPlugin");
                }
                else if (agentMode == AgentMode.ACA)
                {
                    // ACA Agent is configuring the SemanticKernel within the FirstPartyAgent.ACA.Web project
                }

                return kernelBuilder.Build();
            });

            return services;
        }
    }
}
