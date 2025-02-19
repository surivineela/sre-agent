// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Web;

public static class ConfigurationExtensions
{
    public static IServiceCollection AddApplicationConfiguration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // Configure typed settings
        services.Configure<AppSettings>(
            configuration.GetSection(nameof(AppSettings)));

        services.Configure<AzureSettings>(
            configuration.GetSection("Azure"));

        // Add configuration validation
        services.AddOptions<AppSettings>()
            .Bind(configuration.GetSection(nameof(AppSettings)))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AzureSettings>()
            .Bind(configuration.GetSection("Azure"))
            //.ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<KustoSettings>()
            .Bind(configuration.GetSection("Kusto"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<KustoClusterSettings>()
            .Bind(configuration.GetSection("KustoClusters"))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        return services;
    }

    // this is duplicated from SemanticKernelHelper without registering any plugins
    // plguins will be registered into the clone of the kernel on the fly
    public static IServiceCollection ConfigureSemanticKernel(this IServiceCollection services)
    {
        services.AddSingleton<HttpClient>(sp =>
        {
            var overrideApiVersion = "2025-01-01-preview";
            return new HttpClient(new AzureOverrideHandler(overrideApiVersion));
        });

        // Configure Semantic Kernel
        services.AddScoped<Kernel>(sp =>
        {
            var config = sp.GetRequiredService<IConfiguration>();

            var azureSettings = config.GetSection("Azure").Get<AzureSettings>();

            if (azureSettings == null)
            {
                throw new NullReferenceException("Azure settings are required.");
            }

            // Check if the model is a reasoning model that needs API version override
            bool isReasoningModel = azureSettings.OpenAI.DeploymentName.Contains("o1-mini", StringComparison.OrdinalIgnoreCase) ||
                                  azureSettings.OpenAI.DeploymentName.Contains("o3-mini", StringComparison.OrdinalIgnoreCase) ||
                                  azureSettings.OpenAI.DeploymentName.Contains("o1", StringComparison.OrdinalIgnoreCase) ||
                                  azureSettings.OpenAI.DeploymentName.Contains("deepseek-r1", StringComparison.OrdinalIgnoreCase);

            var kernelBuilder = Kernel.CreateBuilder();

            if (isReasoningModel)
            {
                var httpClient = sp.GetRequiredService<HttpClient>();

                kernelBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: azureSettings.OpenAI.DeploymentName,
                    endpoint: azureSettings.OpenAI.Endpoint,
                    apiKey: azureSettings.OpenAI.ApiKey,
                    httpClient: httpClient);
            }
            else
            {
                kernelBuilder.AddAzureOpenAIChatCompletion(
                    deploymentName: azureSettings.OpenAI.DeploymentName,
                    endpoint: azureSettings.OpenAI.Endpoint,
                    apiKey: azureSettings.OpenAI.ApiKey);
            }


            kernelBuilder.Services.AddLogging(builder =>
            {
                // Use configuration for logging levels
                builder.AddConfiguration(config.GetSection("Logging"));
                builder.AddConsole();
            });

            return kernelBuilder.Build();
        });

        return services;
    }
}

