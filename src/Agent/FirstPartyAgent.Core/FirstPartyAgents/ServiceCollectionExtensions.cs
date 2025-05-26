using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Clients;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCorednsAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppSessionsAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCustomerMetricsAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppIngressAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppQuotaAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.HelloWorldAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.RevisionAgent;
using FirstPartyAgent.Core.Plugins;
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
using FirstPartyAgent.Core.Clients;
using FirstPartyAgent.Plugins.Interfaces;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppJobsAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCustomerMetricsAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCustomerLogsAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvironmentAgent;

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

        builder.Services.AddSingleton<IACAKustoPlugin, ACAKustoPlugin>();
        builder.Services.AddSingleton<IKustoPluginChat, KustoPluginChat>();
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
        services.AddSingleton<HelloWorldAgentPlugin>();
        services.AddSingleton<HelloWorldPluginDefinition>();
        services.AddSingleton<HelloWorldAgentFactory>();

        services.AddSingleton<IContainerAppQuotaPlugin, ContainerAppQuotaPlugin>();
        services.AddSingleton<ContainerAppQuotaAgentPlugin>();
        services.AddSingleton<ContainerAppQuotaPluginDefinition>();
        services.AddSingleton<ContainerAppQuotaAgentFactory>();

        services.AddSingleton<IContainerAppJobsPlugin, ContainerAppJobsPlugin>();
        services.AddSingleton<IAzureDocSearchPlugin, AzureDocSearchPlugin>();
        services.AddSingleton<IAzureSearchClient, AzureSearchClient>();
        services.AddSingleton<IContainerAppRevisionPlugin, ContainerAppRevisionPlugin>();
        services.AddSingleton<IManagedClusterPlugin, ManagedClusterPlugin>();
        services.AddSingleton<IManagedEnvironmentPlugin, ManagedEnvironmentPlugin>();
        services.AddSingleton<IHealthProbePlugin, HealthProbePlugin>();
        services.AddSingleton<INodeAvailabilityPlugin, NodeAvailabilityPlugin>();
        services.AddSingleton<ContainerAppJobsAgentPlugin>();
        services.AddSingleton<ContainerAppRevisionAgentPlugin>();
        services.AddSingleton<ContainerAppDocumentSearchPluginDefinition>();
        services.AddSingleton<ContainerAppRevisionPluginDefinition>();
        services.AddSingleton<ContainerAppJobsPluginDefinition>();
        services.AddSingleton<ManagedEnvironmentPluginDefinition>();
        services.AddSingleton<ManagedClusterPluginDefinition>();
        services.AddSingleton<HealthProbePluginDefinition>();
        services.AddSingleton<NodeAvailabilityPluginDefinition>();
        services.AddSingleton<ContainerAppJobsAgentFactory>();
        services.AddSingleton<ContainerAppRevisionAgentFactory>();

        services.AddSingleton<IContainerAppEnvoyPlugin, ContainerAppEnvoyPlugin>();
        services.AddSingleton<ContainerAppIngressAgentPlugin>();
        services.AddSingleton<ContainerAppEnvoyPluginDefinition>();
        services.AddSingleton<ContainerAppIngressAgentFactory>();

        services.AddSingleton<IContainerAppCorednsPlugin, ContainerAppCorednsPlugin>();
        services.AddSingleton<ContainerAppCorednsAgentPlugin>();
        services.AddSingleton<ContainerAppCorednsPluginDefinition>();
        services.AddSingleton<ContainerAppCorednsAgentFactory>();

        services.AddSingleton<IContainerAppSessionsPlugin, ContainerAppSessionsPlugin>();
        services.AddSingleton<ContainerAppSessionsAgentPlugin>();
        services.AddSingleton<ContainerAppSessionsPluginDefinition>();
        services.AddSingleton<ContainerAppSessionsAgentFactory>();

        services.AddSingleton<IIcmPlugin, IcmPlugin>();
        services.AddSingleton<IContainerAppIcMPlugin, ContainerAppIcMPlugin>();
        services.AddSingleton<ContainerAppIcMPluginDefinition>();

        services.AddSingleton<ACAKustoPluginDefinition>();
        services.AddSingleton<ContainerAppsPluginDefinition>();
        services.AddSingleton<IContainerAppsPlugin, ContainerAppsPlugin>();

        services.AddSingleton<IContainerAppCustomerMetricsPlugin, ContainerAppCustomerMetricsPlugin>();
        services.AddSingleton<ContainerAppCustomerMetricsAgentPlugin>();
        services.AddSingleton<ContainerAppCustomerMetricsPluginDefinition>();
        services.AddSingleton<ContainerAppCustomerMetricsAgentFactory>();

        services.AddSingleton<IContainerAppCustomerLogsPlugin, ContainerAppCustomerLogsPlugin>();
        services.AddSingleton<ContainerAppCustomerLogsAgentPlugin>();
        services.AddSingleton<ContainerAppCustomerLogsPluginDefinition>();
        services.AddSingleton<ContainerAppCustomerLogsAgentFactory>();

        services.AddSingleton<ContainerAppEnvironmentAgentPlugin>();
        services.AddSingleton<ContainerAppEnvironmentAgentFactory>();
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
