// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Helpers;
using Agent.Web.Services;
using Agent.Runtime;
using Agent.Core.Models;
using Agent.Runtime.Services;
using Agent.Plugins;
using Agent.Core.Configuration;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Plugins.PeriodicMonitor;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;

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

if (useSessionChatService)
{
    // Configure Azure settings
    builder.Services.Configure<AzureSettings>(
        builder.Configuration.GetSection("Azure"));

    builder.Services.AddLogging();

    // Register plugins and their dependencies
    builder.Services.AddSingleton<ISubscriptionPlugin, SubscriptionPlugin>()
                   .AddSingleton<SubscriptionPluginDefinition>()
                   .AddSingleton<IGraphDatabaseManager, GremlinGraphDatabaseManager>()
                   .AddSingleton<IGraphDBPlugin, GraphDBPlugin>()
                   .AddSingleton<GraphDBPluginDefinition>()
                   .AddSingleton<ITimePlugin, TimePlugin>()
                   .AddSingleton<TimePluginDefinition>()
                   .AddSingleton<IMetricsPlugin, MetricsPlugin>()
                   .AddSingleton<MetricsPluginDefinition>()
                   .AddSingleton<IPeriodicMonitor, PeriodicMonitor>()
                   .AddSingleton<IMonitorPlugin, MonitorPlugin>()
                   .AddSingleton<MonitorPluginDefinition>()
                   .AddSingleton<ICurrentStatePlugin, CurrentStatePlugin>()
                   .AddSingleton<CurrentStatePluginDefinition>()
                   .AddSingleton<IDiagnosePlugin, DiagnosePlugin>()
                   .AddSingleton<DiagnosePluginDefinition>()
                   .AddSingleton<IRemediationPlugin, RemediationPlugin>()
                   .AddSingleton<RemediationPluginDefinition>();

    builder.Services.AddSingleton<IChatHistoryStorage, ChatHistoryStorage>();

    // Configure chat services
    builder.Services.ConfigureIChatCompletionService()
                   .ConfigureAzureOpenAIClient()
                   .ConfigureIChatClient();
    builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri("http://localhost:5073/") });
    // Register all SubAgent types
    foreach (var agentType in SubAgentDiscovery.DiscoverSubAgentTypes())
    {
        builder.Services.AddScoped(agentType);
    }

    // Add agent manager
    builder.Services.AddSingleton<IAgentManager, AgentManager>();
    builder.Services.AddScoped<IChatService, Agent.Web.Services.SessionChatService>();
}
else
{
    SemanticKernelHelper.ConfigService(builder.Services);
    builder.Services.AddScoped<IChatService, LegacyChatService>();
}

// Add services to the container.
builder.Services.AddHttpContextAccessor(); // Add this line
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
app.MapControllers();
app.MapBlazorHub();

// Finally, map the fallback page
app.MapFallbackToPage("/_Host");

app.Run();
