// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Crawler.Metrics;
using Agent.Graph.Interfaces;
using Agent.Graph.Services;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Prometheus.Services;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.ContextManagement;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.AppCodeAnalysisAgent;
using Agent.Runtime.SubAgents.AppReliabilityAgent;
using Agent.Runtime.SubAgents.AppServiceRemediation;
using Agent.Runtime.SubAgents.ContainerAppsRemediation;
using Agent.Runtime.SubAgents.ContainerImagePullFailureAgent;
using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.CPUAnalysisAgent;
using Agent.Runtime.SubAgents.CVEAgent;
using Agent.Runtime.SubAgents.DailyReportSummary;
using Agent.Runtime.SubAgents.FunctionAppConnectivityAgent;
using Agent.Runtime.SubAgents.KubernetesAgent;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Agent.Runtime.SubAgents.SqlDbQueryPerfAgent;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Agent.Runtime.SubAgents.TlsBestPracticesAgent;
using Agent.Runtime.SubAgents.VmRdpInvestigatorAgent;
using Agent.Runtime.SubAgents.WebAppDownAgent;
using Agent.Runtime.TeamsChatServices;
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
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel;
using OpenTelemetry;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.AzureDataExplorer;
using Serilog.Sinks.AzureDataExplorer.Extensions;
using FirstPartyAgent.Core.FirstPartyAgents;

var firstPartySubAgentsFactory = new FirstPartySubAgentsFactory();
var isFirstAgent = firstPartySubAgentsFactory.IsFirstPartyAgent();

var builder = WebApplication.CreateBuilder(args);

builder.LoadAppSettings(builder.Environment.IsDevelopment());
builder.ValidateAndRegisterAppSettings<AppSettings>();

ConfigureLogger();

builder.Host.UseSerilog();

{
    // Configure Azure settings
    builder.Services.Configure<AzureSettings>(
        builder.Configuration.GetSection("Azure"));

    builder.Services.AddLogging();

    builder.Services.AddSingleton<IConfiguration>(builder.Configuration);


    //Configure Azure App Insights settings
    builder.Services.Configure<AppInsightsSettings>(
        builder.Configuration.GetSection("AppInsightsSettings"));

    // Register a default ConversationReference that can be injected into PostToTeamsPlugin
    // builder.Services.AddSingleton<Microsoft.Bot.Schema.ConversationReference>(new Microsoft.Bot.Schema.ConversationReference());

    // Register plugins and their dependencies

    builder.Services
        .AddSingleton<Agent.Runtime.MetaAgent.IAgent, MetaAgent>()
        .AddSingleton<IAppServicePlugin, AppServicePlugin>()
        .AddSingleton<AppServicePluginDefinition>()
        .AddSingleton<IFunctionAppsPlugin, FunctionAppsPlugin>()
        .AddSingleton<FunctionAppsPluginDefinition>()
        .AddSingleton<IGraphDatabaseClient, GremlinGraphDatabaseClient>()

        .AddSingleton<ITimePlugin, TimePlugin>()
        .AddSingleton<IMetricsPlugin, MetricsPlugin>()
        .AddSingleton<IAppInsightsPlugin, AppInsightsPlugin>()
        .AddSingleton<AppInsightsPluginDefinition>()
        .AddSingleton<Agent.Plugins.Models.GitHubClient>()
        .AddSingleton<IGithubIssuePlugin, GitHubIssuePlugin>()
        .AddSingleton<IRemediationPlugin, RemediationPlugin>()
        .AddSingleton<AzureResourceGraphClient>()
        .AddSingleton<ArmHelper>()
        .AddSingleton<ArmResourceCrawlerFactory>()
        .AddSingleton<ICrawlerService, ResourceGraphCrawlerService>()
        .AddSingleton<IContainerImagePullFailurePlugin, ContainerImagePullFailurePlugin>()
        .AddSingleton<IMetaAgentContainerImageTroubleshooterPlugin, ContainerImageTroubleshooterPlugin>()
        .AddSingleton<IReliabilityPlugin, ReliabilityPlugin>()
        .AddSingleton<IMetaAgentAppReliabilityPlugin, AppReliabilityPlugin>()
        .AddSingleton<AppReliabilityAgentFactory>()
        .AddSingleton<AppCodeAnalysisAgentFactory>()
        .AddSingleton<INSGRulePlugin, NSGRulePlugin>()
        .AddSingleton<ContainerAppsRemediationAgentFactory>()
        .AddSingleton<ContainerImagePullFailureAgentFactory>()
        .AddSingleton<IContainerAppPlugin, ContainerAppPlugin>()
        .AddSingleton<IRemoteWriteService, RemoteWriteService>()
        .AddSingleton<AzureSupportCenterHelper>()
        .AddSingleton<IAzureSupportCenterPlugin, AzureSupportCenterPlugin>()
        .AddSingleton<VmRdpInvestigatorAgentFactory>()
        .AddSingleton<IMetaAgentVmRdpInvestigatorPlugin, VmRdpInvestigatorPlugin>()
        .AddSingleton<AppInsightsSettings>()
        .AddSingleton<FunctionAppConnectivityAgentFactory>()
        .AddSingleton<IMetaAgentFunctionAppConnectivityPlugin, FunctionAppConnectivityPlugin>()
        .AddSingleton<IPrometheusQueryService, PrometheusQueryService>()
        .AddSingleton<IRoleAssignmentPlugin, RoleAssignmentPlugin>()

        .AddSingleton<IFirstPartySubAgentsFactory>(firstPartySubAgentsFactory)

        .AddSingleton<SqlDbQueryPerfAgentFactory>()
        .AddSingleton<IMetaAgentSqlDbQueryPerfPlugin, SqlDbQueryPerfPlugin>()

        .AddTransient<MetricsPluginDefinition>()
        .AddTransient<ChartPluginDefinition>()
        .AddTransient<RecordActionsPluginDefinition>()
        .AddTransient<GrafanaPluginDefinition>()
        .AddTransient<GraphDBPluginDefinition>()
        .AddTransient<ArmPluginDefinition>()
        .AddTransient<TimePluginDefinition>()
        .AddTransient<MIConfigurationCheckPluginDefinition>()
        .AddTransient<GithubWorkflowTriggerPluginDefinition>()
        .AddTransient<RemediationPluginDefinition>()
        .AddTransient<AppIdentityUpdatePluginDefinition>()
        .AddTransient<ControlFlowPluginDefinition>()
        .AddTransient<ApprovalPluginDefinition>()
        .AddTransient<NSGRulePluginDefinition>()
        .AddTransient<ContainerAppPluginDefinition>()
        .AddTransient<ReliabilityPluginDefinition>()
        .AddTransient<KubePluginDefinition>()
        .AddTransient<GitHubIssuePluginDefinition>()
        .AddTransient<AzureSupportCenterPluginDefinition>()
        .AddTransient<ContainerImagePullFailurePluginDefinition>()
        .AddTransient<CpuAnalysisPluginDefinition>()
        .AddTransient<AppCodeAnalysisPluginDefinition>()
        .AddTransient<RoleAssignmentPluginDefinition>()

        .AddTransient<IMetaAgentContainerAppsRemediationPlugin, ContainerAppsRemediationPlugin>()
        .AddTransient<IMetaAgentManagedIdentityMigrationPlugin, ManagedIdentityMigrationPlugin>()
        .AddTransient<IMetaAgentTlsBestPracticesPlugin, TlsBestPracticesPlugin>()
        .AddTransient<IMetaAgentKubernetesAgentPlugin, KubernetesAgentPlugin>()
        .AddTransient<IMetaAgentWebAppDownPlugin, WebAppDownPlugin>()
        .AddTransient<IMetaAgentCPUAnalysisPlugin, CPUAnalysisAgentPlugin>()
        .AddTransient<IMetaAgentAppCodeAnalysisPlugin, AppCodeAnalysisAgentPlugin>()
        .AddTransient<IKubePlugin, KubePlugin>()
        .AddTransient<IMetaAgentAppServiceRemediationPlugin, AppServiceRemediationPlugin>()
        .AddTransient<IChartPlugin, ChartPlugin>()
        .AddTransient<ChartPlugin>()
        .AddTransient<IGraphDBPlugin, GraphDBPlugin>()

        .AddSingleton<AppServiceRemediationAgentFactory>()
        .AddSingleton<KubernetesAgentFactory>()
        .AddSingleton<ManagedIdentityMigrationAgentFactory>()
        .AddSingleton<TlsBestPracticeAgentFactory>()
        .AddSingleton<TlsBestPracticesScanner>()
        .AddSingleton<IMetaAgentStorageAccountPlugin, StorageAccountPlugin>()
        .AddSingleton<WebAppDownAgentFactory>()
        .AddSingleton<CPUAnalysisAgentFactory>()
        .AddSingleton<AppCodeAnalysisAgentFactory>()
        .AddSingleton<SourceCodeScanner>()
        .AddSingleton<CVEScanner>()
        .AddSingleton<FeedbackRCAScanner>()
        .AddSingleton<PostToTeamsPluginDefinition>()
        .AddSingleton<DailyReportScanner>()
        .AddSingleton<AppServiceScanner>()
        .AddSingleton<DailyReportSummaryAgentFactory>()
        .AddSingleton<IPostToTeamsPlugin, PostToTeamsPlugin>()
        .AddSingleton<IApprovalPlugin, ApprovalPlugin>()
        .AddSingleton<IArmPlugin, ArmPlugin>()
        .AddSingleton<IConnectedIntegrationsPlugin, ConnectedIntegrationsPlugin>()
        .AddSingleton<IGrafanaPlugin, GrafanaPlugin>()
        .AddSingleton<IRecordActionsPlugin, RecordActionsPlugin>()
        .AddSingleton<IGithubWorkflowTriggerPlugin, GithubWorkflowTriggerPlugin>()
        .AddSingleton<IMIConfigurationCheckPlugin, MIConfigurationCheckPlugin>()
        .AddSingleton<IAppIdentityUpdatePlugin, AppIdentityUpdatePlugin>()
        .AddSingleton<ITimePlugin, TimePlugin>()
        .AddSingleton<McpToolsRepository>()
        .AddSingleton<IThreadOrchestrationManager, CosmosThreadOrchestrationManager>()
        .AddSingleton<SinkService>()
        .AddSingleton<ThreadService>()
        .AddSingleton<IAgentInboundCommunicationService, InboundCommunicationService>()
        .AddSingleton<IAgentOutboundCommunicationService, OutboundCommunicationService>()
        .AddSingleton<IApprovalService, DurableApprovalService>()
        .AddSingleton<IRemoteWriteService, RemoteWriteService>()
        .AddSingleton<IMetricsRegistry, MetricsRegistry>()
        .AddSingleton<IGremlinMetricsService, GremlinMetricsService>()
        .AddSingleton<AppInsightsPlugin>()
        .AddSingleton<ICpuAnalysisPlugin, CpuAnalysisPlugin>()
        .AddSingleton<IAppCodeAnalysisPlugin, AppCodeAnalysisPlugin>()


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
        .AddSingleton<LogsAndMetricsAgent>()

        // Register Metrics collectors
        .AddSingleton<ScoreCardService>()
        .AddSingleton<IAzureMetricsClient, AzureMetricsClient>()
        .AddSingleton<IResourceMetricsCollector, ContainerAppMetricsCollector>()
        .AddSingleton<IResourceMetricsCollector, FunctionAppMetricsCollector>()
        .AddSingleton<IResourceMetricsCollector, AppServiceMetricsCollector>()
        .AddSingleton<IResourceMetricsCollector, RedisMetricsCollector>();

    if (isFirstAgent)
    {
        builder.Services.AddSingleton<IToolsRepository, FirstPartyToolsRepository>();
        builder.RegisterFirstPartySubAgentsDependencies();
    }
    else
    {
        builder.Services.AddSingleton<IToolsRepository, ToolsRepository>();
    }

    // Register all subagent factories that derive from the shared impl
    var genericSubAgentFactories = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(MetaAgent).Assembly, typeof(SimpleResourceSubAgentFactoryBase<,,,>));
    foreach (var type in genericSubAgentFactories)
    {
        builder.Services.AddSingleton(type);
    }
    // Register all subagent plugins that derive from the shared impl
    var genericSubAgentPlugins = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(MetaAgent).Assembly, typeof(SimpleResourceSubAgentPluginBase<,,,,>));
    foreach (var type in genericSubAgentPlugins)
    {
        builder.Services.AddTransient(type);
    }
    // Register all subagent scanners that derive from the shared impl
    var genericSubAgentScanners = TypeReflectionHelpers.GetClassesDerivedFromGeneric(typeof(MetaAgent).Assembly, typeof(SimpleResourceSubAgentScannerBase<,,,>));
    foreach (var type in genericSubAgentScanners)
    {
        builder.Services.AddSingleton(type);
    }

    builder.Services.AddSingleton<IAuthenticationService, AuthenticationService>();
    builder.Services.AddSingleton<IArmClientFactory, ArmClientFactory>();
    builder.Services.AddSingleton<IKubernetesClientFactory, KubernetesClientFactory>();
    builder.Services.AddKeyedSingleton<IKubernetesService, CrawlerKubernetesService>("Crawler");
    builder.Services.AddSingleton<IActivityLogService, ActivityLogService>();

    // Register HttpClientService and configure HttpClient with proper BaseAddress
    builder.Services.AddSingleton<HttpClientService>();
    builder.Services.AddArmHelperHttpClient();
    builder.Services.AddRazorHttpClient();
    builder.Services.AddCrawlerHttpClient();

    // Configure chat services
    builder.Services.ConfigureIChatCompletionService()
                   .ConfigureAzureOpenAIClient()
                   .ConfigureIChatClient();


    // Register all SubAgent types
    foreach (var agentType in SubAgentDiscovery.DiscoverSubAgentTypes())
    {
        builder.Services.AddSingleton(agentType);
    }

    // agent context management
    builder.Services.AddSingleton<AgentContextDispatchService>();
    builder.Services.AddSingleton<AgentContextProcessingService>();
    builder.Services.AddHostedService<InstanceLifetimeService>();

    // Kick off background processes
    if (!isFirstAgent)
    {
        builder.Services.AddHostedService<TimerService>();
    }
    
    // Kick off MCP Server Initializer
    builder.Services.AddSingleton<MCPMetaAgent>();
    builder.Services.AddHostedService<MCPMetaAgentManagementService>();

    builder.Services.AddSingleton<BotFrameworkAuthentication, ConfigurationBotFrameworkAuthentication>();
    builder.Services.AddSingleton<IBotFrameworkHttpAdapter, AdapterWithErrorHandler>();
    builder.Services.AddSingleton<IBot, TeamsBot>()
                    .AddSingleton<IBotPollingMessage, TeamsBot>();
    // Add the new polling service
    builder.Services.AddHostedService<TeamsMessagePollingService>();

    builder.Services.AddDurableTaskWorker(b =>
    {
        b.AddTasks(r =>
        {
            DurableHelper.AddAllGeneratedTasks(r);
            if(isFirstAgent)
            {
                FirstPartyDurableHelper.AddAllGeneratedTasks(r);
            }
        });

        string durableConnectionString = builder.ResolveDtsConnectionString();
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
        string durableConnectionString = builder.ResolveDtsConnectionString();
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

// Add GraphService registration
builder.Services.AddSingleton<IGraphService, GraphService>();

var app = builder.Build();

var metricsService = app.Services.GetRequiredService<IGremlinMetricsService>();
// Kick off metrics collection after the app has fully started
app.Lifetime.ApplicationStarted.Register(() => metricsService.StartMetricsCollection());

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
app.UseDefaultFiles();
app.UseStaticFiles();
app.UseRouting();

app.MapControllers();
app.MapBlazorHub();

// Finally, map the fallback page
app.MapFallbackToFile("/index.html");

var azureSettings = builder.Configuration.GetSection("AppSettings:Core:Azure").Get<AzureSettings>();
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

void ConfigureLogger()
{
    if (builder.Environment.IsDevelopment())
    {
        // Enable Serilog self-logging to output internal errors to the console
        Serilog.Debugging.SelfLog.Enable(Console.Out);
    }

    var loggerConfiguration = new LoggerConfiguration()
            .ReadFrom.Configuration(builder.Configuration)
            .Enrich.WithProperty("AgentName", Environment.GetEnvironmentVariable("AGENT_NAME"));

    var kustoConfig = new Dictionary<string, string>
    {
        { "ClusterUri", GetKustoClusterConfiguration("ClusterUri") },
        { "DatabaseName", GetKustoClusterConfiguration("DatabaseName") },
        { "TableName", GetKustoClusterConfiguration("TableName") }
    };

    if (kustoConfig.Values.All(value => !string.IsNullOrEmpty(value)))
    {
        Log.Logger.Information("Configuring Kusto cluster sink with the following configuration: {KustoConfig}", kustoConfig);
        var kustoSinkOptions = new AzureDataExplorerSinkOptions
        {
            IngestionEndpointUri = kustoConfig["ClusterUri"],
            DatabaseName = kustoConfig["DatabaseName"],
            TableName = kustoConfig["TableName"]
        };

        if (!builder.Environment.IsDevelopment())
        {
            // When running in Azure, use managed identity for authentication else local user auth is used
            var identity = GetKustoClusterConfiguration("Identity");
            kustoSinkOptions = !string.IsNullOrEmpty(identity) && !string.Equals(identity, "System", StringComparison.OrdinalIgnoreCase)
                ? kustoSinkOptions.WithAadUserAssignedManagedIdentity(identity)
                : kustoSinkOptions.WithAadSystemAssignedManagedIdentity();
        }

        loggerConfiguration.WriteTo.AzureDataExplorerSink(kustoSinkOptions);
        Log.Logger = loggerConfiguration.CreateLogger();
        Log.Logger.Information("Configured Kusto cluster sink with the following configuration: {KustoConfig}", kustoConfig);
    }
    else
    {
        Log.Logger = loggerConfiguration.CreateLogger();
        Log.Logger.Warning("Kusto cluster sink is not enabled. Missing configuration: {KustoConfig}", kustoConfig);
    }
}

string GetKustoClusterConfiguration(string key)
{
    const string prefix = "AppSettings__Core__Azure__FirstParty__KustoClusterConfiguration_";
    return Environment.GetEnvironmentVariable($"{prefix}{key}") ?? string.Empty;
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
