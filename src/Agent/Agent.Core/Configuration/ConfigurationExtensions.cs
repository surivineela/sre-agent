// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Agent.Core.Configuration;

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

        services.Configure<KustoSettings>(
            configuration.GetSection("Kusto"));

        services.AddOptions<AppSettings>()
            .Bind(configuration.GetSection(nameof(AppSettings)))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AzureSettings>()
            .Bind(configuration.GetSection("Azure"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<ICMSettings>()
            .Bind(configuration.GetSection("ICM"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<AzureSearchSettings>()
            .Bind(configuration.GetSection("AzureSearch"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        services.AddOptions<KustoSettings>()
            .Bind(configuration.GetSection("Kusto"))
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}

