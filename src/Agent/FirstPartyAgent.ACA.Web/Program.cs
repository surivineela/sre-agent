// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using FirstPartyAgent.ACA.Web.Services;
using FirstPartyAgent.Plugins;
using Agent.Runtime;
using Agent.Core.Configuration;
using FirstPartyAgent.Core.Configuration;
using FirstPartyAgent.ACA.Web.Configuration;
using Microsoft.Extensions.Options;

namespace FirstPartyAgent.ACA.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.LoadAppSettings();
        builder.ValidateAndRegisterAppSettings<FirstPartyAgentACAAppSettings>();
        
        builder.Services.AddOptionsWithValidateOnStart<FirstPartyAgentACAAppSettings>()
        .BindConfiguration("ACA")
        .ValidateDataAnnotations();
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentACAAppSettings>>().Value);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.Kusto);
        builder.Services.AddSingleton(sp => sp.GetRequiredService<IOptions<FirstPartyAgentExternalSettings>>().Value.ICM);

        builder.Configuration.AddJsonFile("aca-kusto.json", optional: true, reloadOnChange: true); //load kusto settings

        builder.Services.ConfigureSemanticKernel();
        // Add services to the container.
        builder.Services.AddSingleton<IIcmPlugin, FirstPartyAgent.Plugins.IcmPlugin>();
        builder.Services.AddScoped<IContainerAppsPlugin, ContainerAppsPlugin>();
        builder.Services.AddScoped<IKustoPlugin, KustoPlugin>();
        builder.Services.AddSingleton<IcmAutomationClient>();
        builder.Services.AddSingleton<ITaskStorageService, FileBasedStorageService>();
        builder.Services.AddScoped<KustoClientService>();
        builder.Services.AddScoped<IQuotaAgentService, QuotaAgentService>();
        builder.Services.AddHostedService<GpuQuotaIcmBackgroundService>();

        builder.Services.AddControllers();
        builder.Services.AddControllersWithViews()
            .AddJsonOptions(options =>
            {
                // Allow HTML in JSON responses
                options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
            });

        // TODO: add authN and authZ
        if (builder.Environment.IsDevelopment())
        {
            // Add Blazor services
            builder.Services.AddRazorPages();
            builder.Services.AddServerSideBlazor();
        }

        var app = builder.Build();

        // Configure the HTTP request pipeline.
        if (!app.Environment.IsDevelopment())
        {
            app.UseExceptionHandler("/Error");
            app.UseHsts();
        }

        app.UseHttpsRedirection();

        // Serve static files from wwwroot
        app.UseStaticFiles();
        app.UseRouting();

        app.UseAuthentication();

        // TODO: add authN and authZ
        if (app.Environment.IsDevelopment())
        {
            app.MapBlazorHub();
            app.MapFallbackToPage("/_Host");
        }

        app.MapControllers();
        app.Run();
    }
}



