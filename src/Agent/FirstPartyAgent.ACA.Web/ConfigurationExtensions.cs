// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Configuration;
using Microsoft.SemanticKernel;
using Agent.Core.Configuration;

namespace FirstPartyAgent.ACA.Web;

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

        services.AddOptions<IcmSettings>()
            .Bind(configuration.GetSection("ICM"))
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

            return kernelBuilder.Build();
        });

        return services;
    }
}
