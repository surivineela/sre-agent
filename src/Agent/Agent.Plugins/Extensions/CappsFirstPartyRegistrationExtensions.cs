using System.Text.Json;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.TeamsPlugin;
using FirstPartyAgent.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using UpgradePluginDefinition = Agent.Plugins.Definitions.PlatformUpgradesPluginDefinition;

#pragma warning disable IDE0130 // extensios should be in the same namespace as the containing type
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // extensios should be in the same namespace as the containing type

public static class CappsFirstPartyRegistrationExtensions
{
    public static void ValidateAndRegisterFirstPartyTypes(this IHostApplicationBuilder builder)
    {
        var secretResolvedEnvConfig = new { };
        builder.Configuration.AddJsonStream(new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(secretResolvedEnvConfig)));

        builder.Services.AddSingleton<IACAKustoPlugin, ACAKustoPlugin>();
        builder.Services.AddSingleton<IKustoPluginChat, KustoPluginChat>();
        builder.Services.AddSingleton<ITeamsClient, TeamsClient>();
        builder.Services.AddSingleton<KustoClient>();
        builder.Services.AddSingleton<KustoRegionalGroupClientProvider>();
        builder.Services.AddSingleton<TeamsClientSettings>();
        builder.Services.AddSingleton<IContainerAppIcMPlugin, ContainerAppIcMPlugin>();
        builder.Services.AddSingleton<ICMWorkflowClient>();
        builder.Services.AddSingleton<UpgradePluginDefinition>();

        builder.Services.AddOptionsWithValidateOnStart<FirstPartyAgent.Common.Configuration.ICMWorkflowSettings>()
            .BindConfiguration("AppSettings:Core:External:ICMWorkflows")
            .ValidateDataAnnotations();

        builder.Services.AddOptionsWithValidateOnStart<KustoSettings>()
            .BindConfiguration("AppSettings:Core:External:Kusto")
            .ValidateDataAnnotations();

        builder.Services.AddOptionsWithValidateOnStart<FirstPartyAgent.Common.Configuration.GeneralSettings>()
        .BindConfiguration("AppSettings:Core:External:GeneralSettings")
        .ValidateDataAnnotations();

        // keep multiple lines for better debugging
        builder.Services.AddSingleton(sp =>
        {
            var icmWorkflowSettings = sp.GetRequiredService<IOptions<FirstPartyAgent.Common.Configuration.ICMWorkflowSettings>>();
            return icmWorkflowSettings.Value;
        });
        builder.Services.AddSingleton(sp =>
        {
            var kustoSettings = sp.GetRequiredService<IOptions<KustoSettings>>();
            return kustoSettings.Value;
        });
        builder.Services.AddSingleton(sp =>
        {
            var generalSettings = sp.GetRequiredService<IOptions<GeneralSettings>>();
            return generalSettings.Value;
        });
    }

    public static void RegisterFirstPartyAppSettings(this IHostApplicationBuilder builder)
    {
        // Load static appsettings which are applicable for ACA 1P RCA Agent.
        builder.Configuration.AddJsonFile("appsettings-aca.json", optional: false, reloadOnChange: true); //load base settings

        // load development setting if env is local
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddJsonFile("appsettings-aca.development.json", optional: true, reloadOnChange: true);
        }
    }
}
