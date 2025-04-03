// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.ACA.Web;

public static class ConfigurationExtensions
{
    // this is duplicated from SemanticKernelHelper without registering any plugins
    // plguins will be registered into the clone of the kernel on the fly
    public static IServiceCollection ConfigureSemanticKernel(this IServiceCollection services)
    {
        // Configure Semantic Kernel
        services.AddScoped((Func<IServiceProvider, Kernel>)(sp =>
        {
            var openAISettings = sp.GetRequiredService<OpenAISettings>();
            var kernelBuilder = Kernel.CreateBuilder();
            kernelBuilder.AddAzureOpenAIChatCompletion(
               deploymentName: openAISettings.LLMDeploymentName,
               endpoint: openAISettings.Endpoint,
               apiKey: openAISettings.ApiKey);


            var config = sp.GetRequiredService<IConfiguration>();
            kernelBuilder.Services.AddLogging(builder =>
            {
                // Use configuration for logging levels
                builder.AddConfiguration(config.GetSection("Logging"));
                builder.AddConsole();
            });

            return kernelBuilder.Build();
        }));

        return services;
    }
}
