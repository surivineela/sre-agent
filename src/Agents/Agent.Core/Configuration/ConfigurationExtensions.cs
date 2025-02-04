using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace Agents.Core.Configuration;

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
            .ValidateDataAnnotations()
            .ValidateOnStart();

        return services;
    }
}

