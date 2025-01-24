using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using OperationalAgentRuntime;
using OperationalAgentRuntime.Cli;
using OperationalAgentRuntime.Cli.DemoExec.Tasks;

namespace OperationalAgentCore;

public static class SemanticKernelHelper
{
    public static void ConfigService(IServiceCollection serviceCollection)
    {
        serviceCollection.AddHostedService<RemediationWorker>();
        serviceCollection.AddSingleton<ITaskClient, TaskClient>();
        serviceCollection.AddScoped<CurrentStatePlugin>();
        serviceCollection.AddScoped<PeriodicRemediationPlugin>();

        // Configure Semantic Kernel
        serviceCollection.AddScoped<Kernel>(sp =>
        {
            var config = sp.GetRequiredService<Microsoft.Extensions.Configuration.IConfiguration>();

            var azureSettings = config.GetSection("Azure").Get<AzureSettings>();

            if (azureSettings == null)
            {
                throw new NullReferenceException("Azure settings are required.");
            }

            var kernelBuilder = Kernel.CreateBuilder()
                .AddAzureOpenAIChatCompletion(
                    deploymentName: azureSettings.OpenAI.DeploymentName,
                    endpoint: azureSettings.OpenAI.Endpoint,
                    apiKey: azureSettings.OpenAI.ApiKey);

            // Register skills
            kernelBuilder.Plugins.AddFromType<MetricsPlugin>("MetricsPlugin");
            kernelBuilder.Plugins.AddFromType<SubscriptionPlugin>("SubscriptionPlugin");
            //kernelBuilder.Plugins.AddFromType<CurrentStatePlugin>("CurrentStatePlugin");
            kernelBuilder.Plugins.AddFromType<RemediationPlugin>("RemediationPlugin");

            var currentStatePlugin = sp.GetRequiredService<CurrentStatePlugin>();
            kernelBuilder.Plugins.AddFromObject(currentStatePlugin, "CurrentStatePlugin");

            var periodicRemPlugin = sp.GetRequiredService<PeriodicRemediationPlugin>();
            kernelBuilder.Plugins.AddFromObject(periodicRemPlugin, "PeriodicRemediationPlugin");

            return kernelBuilder.Build();
        });
    }
}
