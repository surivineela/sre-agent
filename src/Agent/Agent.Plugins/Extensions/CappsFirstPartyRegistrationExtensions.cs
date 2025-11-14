// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.Text.Json;
using Agent.Core.Configuration;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Interface;
using Agent.Plugins.TeamsPlugin;
using Agent.Plugins.Tools;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

#pragma warning disable IDE0130 // extensios should be in the same namespace as the containing type

namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130 // extensios should be in the same namespace as the containing type

public static class CappsFirstPartyRegistrationExtensions
{
    public static void RegisterAcaFirstPartyApp(this IHostApplicationBuilder builder)
    {
        builder.Configuration.AddJsonFile("appsettings-aca.json", optional: false, reloadOnChange: true);

        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddJsonFile("appsettings-aca.development.json", optional: true, reloadOnChange: true);
        }

        var secretResolvedEnvConfig = new { };
        builder.Configuration.AddJsonStream(new MemoryStream(JsonSerializer.SerializeToUtf8Bytes(secretResolvedEnvConfig)));

        builder.Services.AddSingleton<ITeamsClient, TeamsClient>();
        builder.Services.AddSingleton<TeamsClientSettings>();

        builder.Services.AddOptionsWithValidateOnStart<Agent.Core.Configuration.ICMWorkflowSettings>()
            .BindConfiguration("AppSettings:Core:External:ICMWorkflows")
            .ValidateDataAnnotations();

        builder.Services.AddOptionsWithValidateOnStart<AzureSearchSettings>()
            .BindConfiguration("AppSettings:Core:External:AzureSearch")
            .ValidateDataAnnotations();

        builder.Services.AddSingleton(sp =>
        {
            var icmWorkflowSettings = sp.GetRequiredService<IOptions<Agent.Core.Configuration.ICMWorkflowSettings>>();
            return icmWorkflowSettings.Value;
        });

        builder.Services.AddSingleton(sp =>
        {
            var azureSearchSettings = sp.GetRequiredService<IOptions<AzureSearchSettings>>();
            return azureSearchSettings.Value;
        });
    }
}
