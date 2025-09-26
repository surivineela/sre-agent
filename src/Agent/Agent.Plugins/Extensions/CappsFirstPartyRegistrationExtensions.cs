using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Framework.Reasoning.Models;
using Agent.Plugins.Clients;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.TeamsPlugin;
using Agent.Plugins.Tools;
using FirstPartyAgent.Common.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130 // extensios should be in the same namespace as the containing type

namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // extensios should be in the same namespace as the containing type

public static class CappsFirstPartyRegistrationExtensions
{
    public static void RegisterAcaFirstPartyAppSettings(this IHostApplicationBuilder builder)
    {
        // Load static appsettings which are applicable for ACA 1P RCA Agent.
        builder.Configuration.AddJsonFile("appsettings-aca.json", optional: false, reloadOnChange: true); //load base settings

        // load development setting if env is local
        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddJsonFile("appsettings-aca.development.json", optional: true, reloadOnChange: true);
        }
    }
    public static void ValidateAndRegisterAcaFirstPartyTypes(this IHostApplicationBuilder builder)
    {
        var secretResolvedEnvConfig = new { };
        builder.Configuration.AddJsonStream(new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(secretResolvedEnvConfig)));

        builder.Services.AddSingleton<ITeamsClient, TeamsClient>();
        builder.Services.AddSingleton<KustoClient>();
        builder.Services.AddSingleton<IAzureSearchClient, AzureSearchClient>();

        builder.Services.AddSingleton<TeamsClientSettings>();
        builder.Services.AddSingleton<IContainerAppIcMPlugin, ContainerAppIcMPlugin>();
        builder.Services.AddSingleton<ICMWorkflowClient>();
        builder.Services.AddSingleton<ICMWorkflowClient, ICMWorkflowClient>();

        builder.Services.AddOptionsWithValidateOnStart<FirstPartyAgent.Common.Configuration.ICMWorkflowSettings>()
            .BindConfiguration("AppSettings:Core:External:ICMWorkflows")
            .ValidateDataAnnotations();

        builder.Services.AddOptionsWithValidateOnStart<FirstPartyAgent.Common.Configuration.GeneralSettings>()
            .BindConfiguration("AppSettings:Core:External:GeneralSettings")
            .ValidateDataAnnotations();
        builder.Services.AddOptionsWithValidateOnStart<AzureSearchSettings>()
    .BindConfiguration("AppSettings:Core:External:AzureSearch")
    .ValidateDataAnnotations();

        // keep multiple lines for better debugging
        builder.Services.AddSingleton(sp =>
        {
            var icmWorkflowSettings = sp.GetRequiredService<IOptions<FirstPartyAgent.Common.Configuration.ICMWorkflowSettings>>();
            return icmWorkflowSettings.Value;
        });

        builder.Services.AddSingleton(sp =>
        {
            var generalSettings = sp.GetRequiredService<IOptions<GeneralSettings>>();
            return generalSettings.Value;
        });
        // keep multiple lines for better debugging
        builder.Services.AddSingleton(sp =>
        {
            var icmWorkflowSettings = sp.GetRequiredService<IOptions<Agent.Core.Configuration.ICMWorkflowSettings>>();
            return icmWorkflowSettings.Value;
        });
        builder.Services.AddSingleton(sp =>
        {
            var icmWorkflowSettings = sp.GetRequiredService<IOptions<AzureSearchSettings>>();
            return icmWorkflowSettings.Value;
        });
        builder.Services.AddSingleton(sp =>
        {
            var kustoSettings = sp.GetRequiredService<IOptions<KustoConnector>>();
            var kustoConnector = kustoSettings.Value;

            // Override authentication type for development
            if (builder.Environment.IsDevelopment())
            {
                kustoConnector.Auth.AuthenticationType = ConnectorAuthType.User;
            }

            return kustoConnector;
        });
    }
}
