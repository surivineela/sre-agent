// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Net.Http.Headers;
using Agent.Core;
using Agent.Core.Configuration;
using Agent.Core.Interfaces;
using Agent.Data;
using Agent.Plugins.Services;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Agent.Runtime.Services.Adc;

/// <summary>
/// Extension methods for configuring ADC (Azure Dev Compute) services.
/// </summary>
public static class AdcServiceCollectionExtensions
{
    /// <summary>
    /// Adds ADC sandbox integration services to the service collection.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">The configuration.</param>
    /// <returns>The service collection for chaining.</returns>
    public static IServiceCollection AddAdcServices(this IServiceCollection services, IConfiguration configuration)
    {
        var adcSettings = configuration.GetSection("AppSettings:Core:Azure:Adc").Get<AdcSettings>() ?? new AdcSettings();

        // Register HttpClient for ADC API with authorization via AdcManagementAccessTokenHandler
        services.AddHttpClient(Constants.HttpClientForAdcManagement, (sp, client) =>
        {
            client.BaseAddress = new Uri(adcSettings.BaseUrl);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            client.Timeout = TimeSpan.FromMinutes(5);
        }).AddHttpMessageHandler(sp =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            return new Agent.Core.Extensions.AdcManagementAccessTokenHandler(authService);
        });

        // Register ADC API Client (pure REST client)
        services.AddSingleton<IAdcApiClient>(sp =>
        {
            var httpClientFactory = sp.GetRequiredService<IHttpClientFactory>();
            var logger = sp.GetRequiredService<ILogger<AdcApiClient>>();

            return new AdcApiClient(
                httpClientFactory,
                adcSettings,
                logger);
        });

        // Register Agent Sandbox Service (orchestration + persistence)
        services.AddSingleton<IAgentSandboxService>(sp =>
        {
            var adcApiClient = sp.GetRequiredService<IAdcApiClient>();
            var cosmosClient = sp.GetRequiredService<CosmosClient>();
            var cosmosDbSettings = sp.GetRequiredService<CosmosDBSettings>();
            var logger = sp.GetRequiredService<ILogger<AgentSandboxService>>();
            var hostEnvironment = sp.GetRequiredService<IHostEnvironment>();

            return new AgentSandboxService(
                adcApiClient,
                cosmosClient,
                adcSettings,
                cosmosDbSettings,
                logger,
                hostEnvironment);
        });

        // Register remote workspace service
        services.AddSingleton<AdcRemoteWorkspaceService>();

        return services;
    }

    /// <summary>
    /// Ensures the ADC base snapshot is created/updated on application startup.
    /// Call this during application initialization after all services are registered.
    /// </summary>
    /// <param name="serviceProvider">The service provider.</param>
    /// <returns>A task representing the async operation.</returns>
    public static async Task EnsureAdcBaseSnapshotAsync(this IServiceProvider serviceProvider)
    {
        var adcSettings = serviceProvider.GetRequiredService<AdcSettings>();
        if (!adcSettings.Enabled)
        {
            return;
        }

        var logger = serviceProvider.GetRequiredService<ILogger<AgentSandboxService>>();
        var sandboxService = serviceProvider.GetRequiredService<IAgentSandboxService>();

        try
        {
            logger.LogInternalInformation("Ensuring ADC base snapshot on startup...");
            await sandboxService.EnsureBaseSnapshotAsync();
            logger.LogInternalInformation("ADC base snapshot ensured successfully.");
        }
        catch (Exception ex)
        {
            logger.LogInternalError(ex, "Failed to ensure ADC base snapshot on startup. ADC functionality may be limited.");
        }
    }
}
