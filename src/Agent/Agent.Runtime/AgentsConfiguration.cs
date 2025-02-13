using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.Extensions.Configuration;
using Microsoft.SemanticKernel.ChatCompletion;
using Agent.Core.Configuration;
using Microsoft.SemanticKernel.Connectors.AzureOpenAI;
using Agent.Core;
using Agent.Plugins;

namespace Agent.Runtime
{
    public static class AgentsConfigurationExtensions
    {
        public static void ConfigureAgents(this IServiceCollection services)
        {
            _ = services
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
                })
                .AddTransient<Kernel>(sp =>
                {
                    return new Kernel(sp);
                })
                .AddSingleton<ISubscriptionPlugin, MockSubscriptionPlugin>()
                .AddSingleton<SubscriptionPluginDefinition>()
                // Agent is defined by its name, instructions, and the plugins it uses
                // In future we load the agent and conversation from a data store. For now it is all in memory
                .AddSingleton(s =>
                {
                    var agent = new Agent(
                        "main",
                        IssueFinderAgent.SystemMessage,
                        s.GetRequiredService<Kernel>());

                    agent.Kernel.Plugins.AddFromObject(s.GetRequiredService<SubscriptionPluginDefinition>(), "SubscriptionPlugin");

                    return agent;
                }).
                AddSingleton<Session>(s => 
                {
                    var conversation = new Session();
                    conversation.AddAgent(s.GetRequiredService<Agent>());
                    return conversation;
                });
        }
    }
}
