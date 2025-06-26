using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Clients;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Plugins;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Implementation;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using ICMWorkflowSettings = FirstPartyAgent.Core.Configuration.ICMWorkflowSettings;

namespace FirstPartyAgent.Core.FirstPartyAgents;

// This class is responsible for all kind of dependency injections required for the first party sub agents.
public static class ServiceCollectionExtensions
{
    public static void RegisterFirstPartySubAgentsDependencies(this IHostApplicationBuilder builder)
    {
        builder.ValidateAndRegisterFirstPartyAppSettings();
        builder.Services.RegisterFirstPartyPluginDependencies();
        builder.Services.RegisterFirstPartySubAgentPluginImplementationDependencies();
        builder.RegisterFirstPartySubAgents();
    }

    private static void ValidateAndRegisterFirstPartyAppSettings(this IHostApplicationBuilder builder)
    {
        // load development setting if env is local
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddJsonFile("aca-appsettings.development.json", optional: true, reloadOnChange: true);
        }

        // TODO: Load config dynamically
        // 1. Read AppSettings:Core:External.*.* environment variables. Example:  "AppSettings__Core__External__ICMWorkflows__UserToken" : "keyVaultSecretUri"
        // 2. Override specified properties with resolving AKV secret value if config key's value is AKV secret ID
        var secretResolvedEnvConfig = new { };
        builder.Configuration.AddJsonStream(new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(secretResolvedEnvConfig)));

        // ADD hard coded settings for now with keeping default in context of ACA for easy local testing and deployment.
        builder.Services.AddSingleton(new HelloWorldSettings());
        builder.Services.AddSingleton(new RevisionSettings());

        builder.Services.AddSingleton<ITeamsClient, TeamsClient>();
        builder.Services.AddSingleton<KustoClient>();
        builder.Services.AddSingleton<KustoRegionalGroupClientProvider>();
        builder.Services.AddSingleton<TeamsClientSettings>();

        builder.Services.AddOptionsWithValidateOnStart<ICMWorkflowSettings>()
            .BindConfiguration("AppSettings:Core:External:ICMWorkflows")
            .ValidateDataAnnotations();
        builder.Services.AddOptionsWithValidateOnStart<AzureSearchSettings>()
    .BindConfiguration("AppSettings:Core:External:AzureSearch")
    .ValidateDataAnnotations();

        builder.Services.AddOptionsWithValidateOnStart<KustoSettings>()
                .BindConfiguration("AppSettings:Core:External:Kusto")
                .ValidateDataAnnotations();

        // keep multiple lines for better debugging
        builder.Services.AddSingleton(sp =>
        {
            var icmWorkflowSettings = sp.GetRequiredService<IOptions<ICMWorkflowSettings>>();
            return icmWorkflowSettings.Value;
        });
        builder.Services.AddSingleton(sp =>
        {
            var icmWorkflowSettings = sp.GetRequiredService<IOptions<AzureSearchSettings>>();
            return icmWorkflowSettings.Value;
        });
        builder.Services.AddSingleton(sp =>
        {
            var kustoSettings = sp.GetRequiredService<IOptions<KustoSettings>>();
            return kustoSettings.Value;
        });
    }

    private static void RegisterFirstPartyPluginDependencies(this IServiceCollection services)
    {
        // TODO: automatically inject these DI in next iteration
        services.AddSingleton<IHelloWorldPlugin, HelloWorldPlugin>();
        services.AddSingleton<HelloWorldPluginDefinition>();
        services.AddSingleton<IAzureSearchClient, AzureSearchClient>();

        services.AddSingleton<IIcmPlugin, IcmPlugin>();
    }

    private static void RegisterFirstPartySubAgentPluginImplementationDependencies(this IServiceCollection services)
    {
        // TODO: automatically inject these DI in next iteration
        // one should add new sub agents specific dependencies here
        services.AddSingleton<IHelloWorldService, HelloWorldService>();
        services.AddSingleton<IRevisionService, RevisionService>();
        services.AddSingleton<ICMWorkflowClient, ICMWorkflowClient>();
    }

    // !!! Note: no new sub-agent plugin should be added here !!!
    private static void RegisterFirstPartySubAgents(this IHostApplicationBuilder builder)
    {
        // Register all subagent factories that derive from the shared impl
        var genericSubAgentFactories = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(FirstPartyAgentsFactory).Assembly, typeof(SimpleResourceSubAgentFactoryBase<,,,>));
        foreach (var type in genericSubAgentFactories)
        {
            builder.Services.AddSingleton(type);
        }
        // Register all subagent plugins that derive from the shared impl
        var genericSubAgentPlugins = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(FirstPartyAgentsFactory).Assembly, typeof(SimpleResourceSubAgentPluginBase<,,,,>));
        foreach (var type in genericSubAgentPlugins)
        {
            builder.Services.AddTransient(type);
        }
        // Register all subagent scanners that derive from the shared impl
        var genericSubAgentScanners = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(FirstPartyAgentsFactory).Assembly, typeof(SimpleResourceSubAgentScannerBase<,,,>));
        foreach (var type in genericSubAgentScanners)
        {
            builder.Services.AddSingleton(type);
        }
    }
}
