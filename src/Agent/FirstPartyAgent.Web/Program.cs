// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using FirstPartyAgent.Agents;
using FirstPartyAgent.Core.Services;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Runtime;
using FirstPartyAgent.Web.Services;
using FirstPartyAgent.Configuration;

var builder = WebApplication.CreateBuilder(args);

var config = builder.Configuration;
config.SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) //load base settings
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true) //load local settings
            .AddEnvironmentVariables();

bool useSessionChatService = false;
if (args.Length > 0)
{
    if (args[0] == "--session")
    {
        useSessionChatService = true;
    }
}

builder.Services.AddApplicationConfiguration(config);
builder.Services.AddSingleton<IICMAPIClient, ICMAPIClient>();
builder.Services.AddSingleton<ICMWorkflowClient, ICMWorkflowClient>();
builder.Services.AddSingleton<ICMPlugin>();
builder.Services.AddSingleton<KustoServiceClientFactory>();
builder.Services.AddSingleton<IKustoPlugin, KustoPlugin>();

if (useSessionChatService)
{
    builder.Services.ConfigureAgents(ICMAgent.SystemMessage);
    // Add background service that processes the chat conversation
    builder.Services.AddHostedService<SessionService>();
    builder.Services.AddScoped<IChatService, SessionChatService>();
}
else
{
    builder.Services.ConfigureSemanticKernel();
    builder.Services.AddScoped<IChatService, LegacyChatService>();
}

// Add services to the container.
// Register our chat service
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Allow HTML in JSON responses
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

// Add Blazor services
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();

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

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");

app.Run();
