using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCorednsAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppIcmAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvoyAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppsQuotaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.HelloWorldAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Implementation;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Definitions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using System.Text.Json;

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
        // Load static appsettings which are applicable for ACA 1P RCA Agent.
        builder.Configuration.AddJsonFile("aca-appsettings.json", optional: false, reloadOnChange: true); //load base settings
        // load development setting if env is local
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddJsonFile("aca-appsettings.development.json", optional: false, reloadOnChange: true);
        }

        // TODO: Load config dynamically
        // 1. Read AppSettings:Core:External.*.* environment variables. Example:  "AppSettings__Core__External__ICMWorkflows__UserToken" : "keyVaultSecretUri"
        // 2. Override specified properties with resolving AKV secret value if config key's value is AKV secret ID
        var secretResolvedEnvConfig = new { };
        builder.Configuration.AddJsonStream(new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(secretResolvedEnvConfig)));

        // ADD hard coded settings for now with keeping default in context of ACA for easy local testing and deployment.
        builder.Services.AddSingleton(new HelloWorldSettings());
        builder.Services.AddSingleton(new RevisionSettings());

        builder.Services.AddSingleton<IKustoPlugin, KustoPlugin>();
        builder.Services.AddSingleton<IKustoPluginChat, KustoPluginChat>();
        builder.Services.AddSingleton<ITeamsClient, TeamsClient>();
        builder.Services.AddSingleton<KustoClientService>();
        builder.Services.AddSingleton<TeamsClientSettings>();

        builder.Services.AddOptionsWithValidateOnStart<ICMWorkflowSettings>()
            .BindConfiguration("AppSettings:Core:External:ICMWorkflows")
            .ValidateDataAnnotations();

        //.BindConfiguration("AppSettings:FirstPartyAgent:ICMWorkflowSettings")
        //.ValidateDataAnnotations();
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
            var kustoSettings = sp.GetRequiredService<IOptions<KustoSettings>>();
            return kustoSettings.Value;
        });
    }

    private static void RegisterFirstPartyPluginDependencies(this IServiceCollection services)
    {
        // TODO: automatically inject these DI in next iteration
        services.AddSingleton<lHelloWorldPlugin, HelloWorldPlugin>();
        services.AddSingleton<HelloWorldAgentPlugin>();
        services.AddSingleton<HelloWorldPluginDefinition>();
        services.AddSingleton<HelloWorldAgentFactory>();

        services.AddSingleton<IContainerAppQuotaPlugin, ContainerAppQuotaPlugin>();
        services.AddSingleton<ContainerAppsQuotaAgentPlugin>();
        services.AddSingleton<ContainerAppQuotaPluginDefinition>();
        services.AddSingleton<ContainerAppsQuotaAgentFactory>();

        services.AddSingleton<IContainerAppRevisionPlugin, ContainerAppRevisionPlugin>();
        services.AddSingleton<ContainerAppRevisionAgentPlugin>();
        services.AddSingleton<ContainerAppRevisionPluginDefinition>();
        services.AddSingleton<ContainerAppRevisionAgentFactory>();


        services.AddSingleton<IContainerAppEnvoyPlugin, ContainerAppEnvoyPlugin>();
        services.AddSingleton<ContainerAppEnvoyAgentPlugin>();
        services.AddSingleton<ContainerAppEnvoyPluginDefinition>();
        services.AddSingleton<ContainerAppEnvoyAgentFactory>();

        services.AddSingleton<IContainerAppCorednsPlugin, ContainerAppCorednsPlugin>();
        services.AddSingleton<ContainerAppCorednsAgentPlugin>();
        services.AddSingleton<ContainerAppCorednsPluginDefinition>();
        services.AddSingleton<ContainerAppCorednsAgentFactory>();

        services.AddSingleton<IIcmPlugin, IcmPlugin>();
        services.AddSingleton<IContainerAppIcMPlugin, ContainerAppIcMPlugin>();
        services.AddSingleton<ContainerAppIcmAgentPlugin>();
        services.AddSingleton<IcmPluginDefinition>();
        services.AddSingleton<ContainerAppIcMPluginDefinition>();
        services.AddSingleton<ContainerAppIcmAgentFactory>();


        services.AddSingleton<KustoPluginDefinition>();
        services.AddSingleton<ContainerAppsPluginDefinition>();
        services.AddSingleton<IContainerAppsPlugin, ContainerAppsPlugin>();
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
