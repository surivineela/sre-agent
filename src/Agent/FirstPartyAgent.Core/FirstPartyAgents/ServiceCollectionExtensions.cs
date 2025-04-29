using Agent.Core.Helpers;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.SubAgents;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.Core.Plugins.Definitions;
using FirstPartyAgent.Core.Plugins.Implementation;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins.Definitions;
using FirstPartyAgent.Plugins;
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

        builder.Services.AddOptionsWithValidateOnStart<ICMWorkflowSettings>()
            .BindConfiguration("AppSettings:Core:External:ICMWorkflows")
            .ValidateDataAnnotations();

        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<ICMWorkflowSettings>>().Value);
    }

    private static void RegisterFirstPartyPluginDependencies(this IServiceCollection services)
    {
        // TODO: automatically inject these DI in next iteration
        // one should add new fundamental plugin dependencies here
        // Note: Don't add sub-agent plugin like 'HelloWorldAgentPlugin' which is automatically loaded.
        services.AddSingleton<lHelloWorldPlugin, HelloWorldPlugin>();
        services.AddSingleton<IcmPluginDefinition>();
        services.AddSingleton<IIcmPlugin, IcmPlugin>();
        services.AddSingleton<ContainerAppsPluginDefinition>();
        services.AddSingleton<IContainerAppsPlugin, ContainerAppsPlugin>();
    }

    private static void RegisterFirstPartySubAgentPluginImplementationDependencies(this IServiceCollection services)
    {
        // TODO: automatically inject these DI in next iteration
        // one should add new sub agents specific dependencies here
        services.AddSingleton<IHelloWorldService, HelloWorldService>();
        services.AddSingleton<ICMWorkflowClient, ICMWorkflowClient>();
    }

    // !!! Note: no new sub-agent plugin should be added here !!!
    private static void RegisterFirstPartySubAgents(this IHostApplicationBuilder builder)
    {
        // Register all subagent factories that derive from the shared impl
        var genericSubAgentFactories = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(FirstPartySubAgentsFactory).Assembly, typeof(SimpleResourceSubAgentFactoryBase<,,,>));
        foreach (var type in genericSubAgentFactories)
        {
            builder.Services.AddSingleton(type);
        }
        // Register all subagent plugins that derive from the shared impl
        var genericSubAgentPlugins = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(FirstPartySubAgentsFactory).Assembly, typeof(SimpleResourceSubAgentPluginBase<,,,,>));
        foreach (var type in genericSubAgentPlugins)
        {
            builder.Services.AddTransient(type);
        }
        // Register all subagent scanners that derive from the shared impl
        var genericSubAgentScanners = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(FirstPartySubAgentsFactory).Assembly, typeof(SimpleResourceSubAgentScannerBase<,,,>));
        foreach (var type in genericSubAgentScanners)
        {
            builder.Services.AddSingleton(type);
        }
    }
}
