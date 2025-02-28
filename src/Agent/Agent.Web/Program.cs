// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Data.DatabaseManagers.GraphDatabase;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Plugins.CodeAnalyzer;
using Agent.Plugins.PeriodicMonitor;
using Agent.Runtime;
using Agent.Runtime.Services;
using Agent.Web;
using Agent.Web.Services;
using Azure.Monitor.OpenTelemetry.Exporter;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

var builder = WebApplication.CreateBuilder(args);

builder.LoadAppSettings();
builder.ValidateAndRegisterAppSettings<AppSettings>();

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
                   .AddSingleton<ICodeAnalyzerPlugin, CodeAnalyzerPlugin>()
                   .AddSingleton<CodeAnalyzerPluginDefinition>()
                   .AddSingleton<CodeAnalyzerService>()
                   .AddSingleton<Agent.Plugins.Models.GitHubClient>()
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

// Register TeamsConnector service
builder.Services.AddSingleton<TeamsConnector>();

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

var azureSettings = builder.Configuration.GetSection("Azure").Get<AzureSettings>();
var loggingSettings = builder.Configuration.GetSection("Logging").Get<LoggingSettings>();

app.Run();

ResourceBuilder resourceBuilder = ResourceBuilder
    .CreateDefault()
    .AddService(serviceName: builder.Environment.ApplicationName, serviceVersion: "0.0.1")
    .AddAttributes(new Dictionary<string, object>
    {
        ["environment.name"] = builder.Environment.EnvironmentName,
        ["team.name"] = "backend"
    });

using TracerProvider tracerProvider = GetTracerProvider(resourceBuilder, azureSettings, loggingSettings);
using MeterProvider meterProvider = GetMeterProvider(resourceBuilder, azureSettings);

TracerProvider GetTracerProvider(ResourceBuilder resourceBuilder, AzureSettings azureSettings, LoggingSettings? loggingSettings)
{
    TracerProviderBuilder builder = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(resourceBuilder)
        .AddSource("Microsoft.SemanticKernel*");

    if (loggingSettings?.LogGenAICalls == true)
    {
        builder = builder.AddConsoleExporter();
    }

    if (!string.IsNullOrEmpty(azureSettings.AppInsights.ConnectionString))
    {
        builder = builder.AddAzureMonitorTraceExporter(options => options.ConnectionString = azureSettings.AppInsights.ConnectionString);
    }

    return builder.Build();
}

MeterProvider GetMeterProvider(ResourceBuilder resourceBuilder, AzureSettings azureSettings)
{
    MeterProviderBuilder builder = Sdk.CreateMeterProviderBuilder()
        .SetResourceBuilder(resourceBuilder)
        .AddMeter("Microsoft.SemanticKernel*");

    if (!string.IsNullOrEmpty(azureSettings.AppInsights.ConnectionString))
    {
        builder = builder.AddAzureMonitorMetricExporter(options => options.ConnectionString = azureSettings.AppInsights.ConnectionString);
    }

    return builder.Build();
}

ILoggerFactory GetLoggerFactory(ResourceBuilder resourceBuilder, AzureSettings azureSettings)
{
    return LoggerFactory.Create(builder =>
    {
        // Add OpenTelemetry as a logging provider
        builder.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            //options.AddConsoleExporter(); // Too verbose to even warrant making toggleable via config, but this is how you do it
            if (!string.IsNullOrEmpty(azureSettings.AppInsights.ConnectionString))
            {
                options.AddAzureMonitorLogExporter(options => options.ConnectionString = azureSettings.AppInsights.ConnectionString);
            }
            // Format log messages. This is default to false.
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });
    });
}