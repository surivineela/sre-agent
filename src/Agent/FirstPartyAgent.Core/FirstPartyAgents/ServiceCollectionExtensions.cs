using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.FirstPartySubAgentPlugins.ACA;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppCorednsAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppIcmAgent;
using FirstPartyAgent.Core.FirstPartySubAgents.ACA.ContainerAppEnvoyAgent;
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
        builder.Configuration.AddJsonFile("aca-kusto.json", optional: false, reloadOnChange: true); //load base settings
    }

    private static void ValidateAndRegisterFirstPartyAppSettings(this IHostApplicationBuilder builder)
    {
        // We will read from 'firstPartyConfiguration' setting passed from Control plane resource and load those config sections dynamically
        // TODO: automatically inject these DI for all kind of required 1P settings in next iteration
        //builder.Services.AddOptionsWithValidateOnStart<HelloWorldSettings>()
        //    .BindConfiguration("AppSettings:Core:External.HelloWorldSettings")
        //    .ValidateDataAnnotations();

        // ADD hard coded settings for now with keeping default in context of ACA for easy local testing and deployment.
        builder.Services.AddSingleton(new HelloWorldSettings());
        builder.Services.AddSingleton(new RevisionSettings());

        builder.Services.AddSingleton<IKustoPlugin, KustoPlugin>();
        builder.Services.AddSingleton<ITeamsClient, TeamsClient>();
        builder.Services.AddSingleton<KustoClientService>();
        builder.Services.AddSingleton<TeamsClientSettings>();

        builder.Services.AddOptionsWithValidateOnStart<ICMWorkflowSettings>()
            .BindConfiguration("AppSettings:Core:External:ICMWorkflows")
            .ValidateDataAnnotations();

        //.BindConfiguration("AppSettings:FirstPartyAgent:ICMWorkflowSettings")
        //.ValidateDataAnnotations();
        builder.Services.AddOptionsWithValidateOnStart<KustoSettings>()
                .BindConfiguration("AppSettings:External:Kusto")
                .ValidateDataAnnotations();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ICMWorkflowSettings>>().Value);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<KustoSettings>>().Value);
    }

    private static void RegisterFirstPartyPluginDependencies(this IServiceCollection services)
    {
        // TODO: automatically inject these DI in next iteration
        services.AddSingleton<lHelloWorldPlugin, HelloWorldPlugin>();
        services.AddSingleton<HelloWorldAgentPlugin>();
        services.AddSingleton<HelloWorldPluginDefinition>();
        services.AddSingleton<HelloWorldAgentFactory>();

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
        services.AddSingleton<ContainerAppIcmAgentPlugin>();
        services.AddSingleton<IcmPluginDefinition>();
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
