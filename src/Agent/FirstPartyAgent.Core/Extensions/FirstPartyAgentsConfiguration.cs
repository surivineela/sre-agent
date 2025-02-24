using Agent.Core.Configuration;
using Agent.Runtime;
using Azure.AI.OpenAI;
using Microsoft.Extensions.AI;
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
}
