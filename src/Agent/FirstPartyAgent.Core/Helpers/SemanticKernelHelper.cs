// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using FirstPartyAgent.Configuration;
using FirstPartyAgent.Models;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Helpers;

public static class SemanticKernelHelper

{
    public static void ConfigService(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<ICMFunctionAppPlugin>();
        serviceCollection.AddScoped<GenericKustoPlugin>();

        // Configure Semantic Kernel
        serviceCollection.AddScoped<Kernel>(sp =>
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

            string agentModeStr = config.GetValue("AgentMode", string.Empty);
            var agentMode = Enum.TryParse<AgentMode>(agentModeStr, out var mode) ? mode : AgentMode.ICM;

            if (agentMode == AgentMode.ICM)
            {
                var icmPlugin = sp.GetRequiredService<ICMFunctionAppPlugin>();
                kernelBuilder.Plugins.AddFromObject(icmPlugin, "IcmPlugin");
                var kustoPlugin = sp.GetRequiredService<GenericKustoPlugin>();
                kernelBuilder.Plugins.AddFromObject(kustoPlugin, "KustoPlugin");
            }
            else if (agentMode == AgentMode.ACA)
            {
                // ACA Agent is configuring the SemanticKernel within the FirstPartyAgent.ACA.Web project
            }

            return kernelBuilder.Build();
        });
    }
}

