// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler.ARM;
using Agent.Plugins;
using Agent.Plugins.CodeAnalyzer;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Plugins.PeriodicMonitor;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.AppServiceRemediation;
using Agent.Runtime.SubAgents.ContainerAppsRemediation;
using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.DailyReportSummary;
using Agent.Runtime.SubAgents.CVEAgent;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Agent.Runtime.SubAgents.StorageAccountAgent;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Agent.Runtime.SubAgents.TlsBestPracticesAgent;
using Agent.Runtime.TeamsChatServices;
using Agent.Seb.Services;
using Agent.Web.Services;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Bot.Builder;
using Microsoft.Bot.Builder.Integration.AspNet.Core;
using Microsoft.Bot.Connector.Authentication;
using Microsoft.DurableTask;
using Microsoft.DurableTask.Client;
using Microsoft.DurableTask.Client.AzureManaged;
using Microsoft.DurableTask.Worker;
using Microsoft.DurableTask.Worker.AzureManaged;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.AzureDataExplorer;
using Serilog.Sinks.AzureDataExplorer.Extensions;

var builder = WebApplication.CreateBuilder(args);

builder.LoadAppSettings(builder.Environment.IsDevelopment());
builder.ValidateAndRegisterAppSettings<AppSettings>();

// Enable Serilog self-logging to output internal errors to the console
Serilog.Debugging.SelfLog.Enable(Console.Out);

// Configure logging
var loggerConfiguration = new LoggerConfiguration()
        .ReadFrom.Configuration(builder.Configuration);

if (!builder.Environment.IsDevelopment())
{
    // Additional changes for production
    loggerConfiguration
        .Enrich.WithProperty("AgentName", Environment.GetEnvironmentVariable("AGENT_NAME"))
        .WriteTo.AzureDataExplorerSink(
            new AzureDataExplorerSinkOptions
            {
                // Temporary - hardcoded values for testing
                IngestionEndpointUri = "https://sub-agent-test.canadacentral.kusto.windows.net",
                DatabaseName = "subagent",
                TableName = "SreAgentLogs"
            }
            .WithAadSystemAssignedManagedIdentity()
        );
}

Log.Logger = loggerConfiguration.CreateLogger();

builder.Host.UseSerilog();

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

    builder.Services.AddSingleton<IConfiguration>(builder.Configuration);


    // Register a default ConversationReference that can be injected into PostToTeamsPlugin
    // builder.Services.AddSingleton<Microsoft.Bot.Schema.ConversationReference>(new Microsoft.Bot.Schema.ConversationReference());

    // Register plugins and their dependencies
    builder.Services
        .AddSingleton<Agent.Runtime.MetaAgent.IAgent, MetaAgent>()
        .AddSingleton<ISubscriptionPlugin, SubscriptionPlugin>()
        .AddSingleton<SubscriptionPluginDefinition>()
        .AddSingleton<IKubePlugin, KubePlugin>()
        .AddSingleton<KubePluginDefinition>()
        .AddSingleton<IGraphDatabaseClient, GremlinGraphDatabaseClient>()
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
        .AddSingleton<IGithubIssuePlugin, GitHubIssuePlugin>()
        .AddSingleton<GitHubIssuePluginDefinition>()
        .AddSingleton<IDiagnosePlugin, DiagnosePlugin>()
        .AddSingleton<DiagnosePluginDefinition>()
        .AddSingleton<IRemediationPlugin, RemediationPlugin>()
        .AddSingleton<AzureResourceGraphClient>()
        .AddSingleton<ArmHelper>()
        .AddSingleton<ArmResourceCrawlerFactory>()
        .AddSingleton<ResourceGraphCrawler>()
        .AddSingleton<RemediationPluginDefinition>()
        .AddSingleton<IChatHistoryStorage, ChatHistoryStorage>()
        .AddSingleton<ContainerAppsRemediationAgentFactory>()
        .AddSingleton<IContainerAppPlugin, ContainerAppPlugin>()
        .AddSingleton<IGraphDbService, GraphDbService>()

        .AddTransient<ContainerAppsRemediationPlugin>()
        .AddTransient<ManagedIdentityMigrationPlugin>()
        .AddTransient<TlsBestPracticesPlugin>()
        .AddTransient<SourceCodePlugin>()
        .AddTransient<AppServiceRemediationPlugin>()
        .AddTransient<StorageAccountPlugin>()
        .AddTransient<IChartPlugin, ChartPlugin>()
        .AddTransient<ChartPlugin>()

        .AddSingleton<AppServiceRemediationAgentFactory>()
        .AddSingleton<StorageAccountAgentFactory>()
        .AddSingleton<ManagedIdentityMigrationAgentFactory>()
        .AddSingleton<TlsBestPracticeAgentFactory>()
        .AddSingleton<TlsBestPracticesScanner>()
        .AddSingleton<SourceCodeAgentFactory>()
        .AddSingleton<SourceCodeScanner>()
        .AddSingleton<CVEAgentFactory>()
        .AddSingleton<CVEScanner>()
        .AddSingleton<PostToTeamsPluginDefinition>()
        .AddSingleton<DailyReportScanner>()
        .AddSingleton<DailyReportSummaryAgentFactory>()
        .AddSingleton<IPostToTeamsPlugin, PostToTeamsPlugin>()
        .AddSingleton<IApprovalPlugin, ApprovalPlugin>()
        .AddSingleton<IArmPlugin, ArmPlugin>()
        .AddSingleton<IGrafanaPlugin, GrafanaPlugin>()
        .AddSingleton<IRecordActionsPlugin, RecordActionsPlugin>()
        .AddSingleton<IGithubWorkflowTriggerPlugin, GithubWorkflowTriggerPlugin>()
        .AddSingleton<IMIConfigurationCheckPlugin, MIConfigurationCheckPlugin>()
        .AddSingleton<IAppIdentityUpdatePlugin, AppIdentityUpdatePlugin>()
        .AddSingleton<ITimePlugin, TimePlugin>()
        .AddSingleton<MetaAgentPlugin>()
        .AddSingleton<ToolsRepository>()
        .AddSingleton<McpToolsRepository>()
        .AddSingleton<IThreadOrchestrationManager, CosmosThreadOrchestrationManager>()
        .AddSingleton<SinkService>()
        .AddSingleton<ThreadService>()
        .AddSingleton<IAgentInboundCommunicationService, InboundCommunicationService>()
        .AddSingleton<IAgentOutboundCommunicationService, OutboundCommunicationService>()
        .AddSingleton<IApprovalService, DurableApprovalService>()

        // Register the communication activities
        .AddSingleton<UpdateThreadWithAgentMessageActivity>()
        .AddSingleton<NotifyCompletionActivity>()
        .AddSingleton<Octokit.IGitHubClient>(provider =>
        {
            var client = new Octokit.GitHubClient(new Octokit.ProductHeaderValue("AzureSreAgent"));
            return client;
        })
        .AddTransient<Kernel>(sp => new Kernel(sp))
        // Register all SubAgent types as singletons
        .AddSingleton<GraphDBQueryAgent>()
        .AddSingleton<ArchitectureAgent>()
        .AddSingleton<GenericAgent>()
        .AddSingleton<LogsAndMetricsAgent>()
        .AddSingleton<DiagnosticAgent>();


    builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
    builder.Services.AddSingleton<IArmClientFactory, ArmClientFactory>();

    // Register HttpClientService and configure HttpClient with proper BaseAddress
    builder.Services.AddSingleton<HttpClientService>();
    builder.Services.AddArmHelperHttpClient();
    builder.Services.AddRazorHttpClient();

    builder.Services.AddSingleton<IChatHistoryStorage, ChatHistoryStorage>();

    // Configure chat services
    builder.Services.ConfigureIChatCompletionService()
                   .ConfigureAzureOpenAIClient()
                   .ConfigureIChatClient();


    // Register all SubAgent types
    foreach (var agentType in SubAgentDiscovery.DiscoverSubAgentTypes())
    {
        builder.Services.AddSingleton(agentType);
    }

    builder.Services.AddSingleton<IAgentManager, AgentManager>();

    builder.Services.AddSingleton<IChatService, SessionChatService>();

    // Kick off background processes
    builder.Services.AddHostedService<TimerService>();

    // Kick off MCP Server Initializer
    builder.Services.AddSingleton<MCPMetaAgent>();
    builder.Services.AddHostedService<MCPMetaAgentManagementService>();

    builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();
    builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
    builder.Services.AddSingleton<IBot, TeamsBot>();

    builder.Services.AddDurableTaskWorker(b =>
    {
        b.AddTasks(r =>
        {
            DurableHelper.AddAllGeneratedTasks(r);
        });

        var azureSettings = builder.Configuration.GetSection("AppSettings")
                     .GetSection("Core")
                     .GetSection("Azure")
                     .Get<AzureSettings>();

        string durableConnectionString =
            azureSettings?.DTS.ConnectionString ?? "Endpoint=http://localhost:14280;TaskHub=default;Authentication=None";
        if (!string.IsNullOrEmpty(azureSettings?.DTS.ConnectionString))
        {
            durableConnectionString = azureSettings.DTS.ConnectionString;
        }

        b.UseDurableTaskScheduler(durableConnectionString);

        builder.Services.AddOptions<DurableTaskSchedulerWorkerOptions>(b.Name).Configure<IServiceProvider>((option, sp) =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            var tokenCredential = authService.GetDtsCredential();

            option.Credential = tokenCredential;
        });
    });

    builder.Services.AddDurableTaskClient(b =>
    {
        var azureSettings = builder.Configuration.GetSection("AppSettings")
                     .GetSection("Core")
                     .GetSection("Azure")
                     .Get<AzureSettings>();

        string durableConnectionString =
            azureSettings?.DTS.ConnectionString ?? "Endpoint=http://localhost:14280;TaskHub=default;Authentication=None";
        if (!string.IsNullOrEmpty(azureSettings?.DTS.ConnectionString))
        {
            durableConnectionString = azureSettings.DTS.ConnectionString;
        }

        b.UseDurableTaskScheduler(durableConnectionString);

        builder.Services.AddOptions<DurableTaskSchedulerClientOptions>(b.Name).Configure<IServiceProvider>((option, sp) =>
        {
            var authService = sp.GetRequiredService<IAuthenticationService>();
            var tokenCredential = authService.GetDtsCredential();

            option.Credential = tokenCredential;
        });
    });

    builder.Services.AddCosmosClient();
}
else
{
    SemanticKernelHelper.ConfigService(builder.Services);
    builder.Services.AddSingleton<IChatService, LegacyChatService>();
}

// Register TeamsConnector service
builder.Services.AddSingleton<TeamsConnector>();

// Add services to the container.
builder.Services.AddHttpContextAccessor();
builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        // Allow HTML in JSON responses
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
        // Convert enum values as strings
        options.JsonSerializerOptions.Converters.Add(new System.Text.Json.Serialization.JsonStringEnumConverter());
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

// Add CORS support for Azure Portal domains
app.UseCors(x => x.WithOrigins(GetAzurePortalDomains(builder.Configuration))
                  .AllowAnyHeader()
                  .AllowCredentials()
                  .SetIsOriginAllowedToAllowWildcardSubdomains());

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

await app.Services.CreateCosmosContainerIfNotExists(builder.Configuration);

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

// Helper method to get Azure Portal domains
static string[] GetAzurePortalDomains(IConfiguration configuration)
{
    string azurePortalDomains = "";
    var configDomains = configuration.GetValue<string>("AppSettings:AzurePortalDomains");
    if (!string.IsNullOrEmpty(configDomains))
    {
        azurePortalDomains = configDomains;
    }

    return azurePortalDomains.Split(',');
}
