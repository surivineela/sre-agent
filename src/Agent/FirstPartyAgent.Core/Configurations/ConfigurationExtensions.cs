// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace FirstPartyAgent.Configuration;

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
}

