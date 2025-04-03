// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Runtime;
using FirstPartyAgent.ACA.Web.Services;
using FirstPartyAgent.Core.Extensions;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.ACA.Web;

public class Program
{
    public static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);
        builder.LoadAppSettings(builder.Environment.IsDevelopment());
        //builder.ValidateAndRegisterAppSettings<FirstPartyAgentAppSettings>();

        builder.Services.RegisterFirstPartyAppSettings();

        builder.Services.ConfigureSemanticKernel();

        // Add services to the container.
        builder.Services.AddSingleton<IIcmPlugin, IcmPlugin>();
        builder.Services.AddSingleton<IContainerAppsPlugin, ContainerAppsPlugin>();
        builder.Services.AddSingleton<KustoClientService>();
        builder.Services.AddSingleton<IKustoPlugin, KustoPlugin>();
        builder.Services.AddSingleton<ICMWorkflowClient>();
        builder.Services.AddSingleton<ITaskStorageService, FileBasedStorageService>();        
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



