// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Core.Extensions;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models.Api.v1;
using Agent.Core.Services;
using Agent.Data;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Framework;
using Agent.Graph.Crawler;
using Agent.Graph.Crawler.ARM;
using Agent.Graph.Crawler.Metrics;
using Agent.Graph.Interfaces;
using Agent.Graph.Services;
using Agent.Logging;
using Agent.Plugins;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Plugins.Implementation.DiagnosticsPlugin;
using Agent.Prometheus.Services;
using Agent.Runtime;
using Agent.Runtime.Communication;
using Agent.Runtime.ContextManagement;
using Agent.Runtime.HelperAgents;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.MetaAgent.Interfaces;
using Agent.Runtime.MetaAgent.SubAgentPlugins;
using Agent.Runtime.Reasoning;
using Agent.Runtime.Services;
using Agent.Runtime.SubAgents;
using Agent.Runtime.SubAgents.AppCodeAnalysisAgent;
using Agent.Runtime.SubAgents.AppReliabilityAgent;
//using Agent.Runtime.SubAgents.AppServiceRemediation;
using Agent.Runtime.SubAgents.AzMonitorAlertAgent;
using Agent.Runtime.SubAgents.ContainerAppsRemediation;
using Agent.Runtime.SubAgents.Core;
using Agent.Runtime.SubAgents.CPUAnalysisAgent;
using Agent.Runtime.SubAgents.CVEAgent;
using Agent.Runtime.SubAgents.DailyReportSummary;
using Agent.Runtime.SubAgents.FeedbackRCAAgent;
using Agent.Runtime.SubAgents.FunctionAppConfigurationCheck;
using Agent.Runtime.SubAgents.FunctionAppConnectivityAgent;
using Agent.Runtime.SubAgents.FunctionAppDiagnosticsAgent;
using Agent.Runtime.SubAgents.FunctionAppExecutionFailuresAgent;
using Agent.Runtime.SubAgents.KubernetesAgent;
using Agent.Runtime.SubAgents.ManagedIdentityMigration;
using Agent.Runtime.SubAgents.PagerDutyAgent;
using Agent.Runtime.SubAgents.SourceCodeAgent;
using Agent.Runtime.SubAgents.SqlDbQueryPerfAgent;
using Agent.Runtime.SubAgents.TlsBestPractices;
using Agent.Runtime.SubAgents.TlsBestPracticesAgent;
using Agent.Runtime.SubAgents.VmRdpInvestigatorAgent;
using Agent.Runtime.SubAgents.WebAppDownAgent;
using Agent.Runtime.TeamsChatServices;
using Agent.Web.Services;
using Azure.Monitor.OpenTelemetry.Exporter;
using FirstPartyAgent.Core.FirstPartyAgents;
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


var isFirstAgent = (Environment.GetEnvironmentVariable("IS_FIRST_PARTY") ?? String.Empty).Trim().ToLower() switch
{
    "true" or "1" or "y" => true,
    "false" or "0" or "n" => false,
    _ => false // Default to false if the value is invalid or not set
};

var builder = WebApplication.CreateBuilder(args);

builder.LoadAppSettings(builder.Environment.IsDevelopment());
builder.ValidateAndRegisterAppSettings<AppSettings>();

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
        .AddTransient<IGithubIssuePlugin, GitHubIssuePlugin>()
        .AddSingleton<IRemediationPlugin, RemediationPlugin>()
        .AddSingleton<AzureResourceGraphClient>()
        .AddSingleton<ArmHelper>()
        .AddSingleton<AzureMonitorMetricsHelper>()
        .AddSingleton<ArmResourceCrawlerFactory>()
        .AddSingleton<ICrawlerService, ResourceGraphCrawlerService>()
        .AddSingleton<IReliabilityPlugin, ReliabilityPlugin>()
        .AddTransient<IMetaAgentAppReliabilityPlugin, AppReliabilityPlugin>()
        .AddSingleton<AppReliabilityAgentFactory>()
        .AddSingleton<AppCodeAnalysisAgentFactory>()
        .AddSingleton<INSGRulePlugin, NSGRulePlugin>()
        .AddSingleton<ContainerAppsRemediationAgentFactory>()
        .AddSingleton<IContainerAppPlugin, ContainerAppPlugin>()
        .AddSingleton<IRemoteWriteService, RemoteWriteService>()
        .AddSingleton<AzureSupportCenterHelper>()
        .AddSingleton<IAzureSupportCenterPlugin, AzureSupportCenterPlugin>()
        .AddSingleton<VmRdpInvestigatorAgentFactory>()
        .AddTransient<IMetaAgentVmRdpInvestigatorPlugin, VmRdpInvestigatorPlugin>()
        .AddSingleton<AppInsightsSettings>()
        .AddSingleton<FunctionAppConnectivityAgentFactory>()
        .AddTransient<IMetaAgentFunctionAppConnectivityPlugin, FunctionAppConnectivityPlugin>()
        .AddSingleton<FunctionAppExecutionFailuresAgentFactory>()
        .AddTransient<IMetaAgentFunctionAppExecutionFailuresAgentPlugin, FunctionAppExecutionFailuresAgentPlugin>()
        .AddSingleton<IPrometheusQueryService, PrometheusQueryService>()
        .AddSingleton<IRoleAssignmentPlugin, RoleAssignmentPlugin>()

        .AddSingleton<SqlDbQueryPerfAgentFactory>()
        .AddTransient<IMetaAgentSqlDbQueryPerfPlugin, SqlDbQueryPerfPlugin>()

        .AddTransient<IMetaAgentFunctionAppDiagnosticsPlugin, FunctionAppDiagnosticsPlugin>()
        .AddSingleton<FunctionAppDiagnosticsAgentFactory>()

        .AddSingleton<FunctionAppConfigurationCheckAgentFactory>()
        .AddTransient<IFunctionAppConfigurationChecksPlugin, FunctionAppConfigurationChecksPlugin>()
        .AddTransient<IMetaAgentFunctionAppConfigurationCheckAgentPlugin, FunctionAppConfigurationCheckPlugin>()

        .AddTransient<MetricsPluginDefinition>()
        .AddTransient<AzureMonitorMetricsPluginDefinition>()
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
        .AddTransient<NSGRulePluginDefinition>()
        .AddTransient<ContainerAppPluginDefinition>()
        .AddTransient<ReliabilityPluginDefinition>()
        .AddTransient<KubePluginDefinition>()
        .AddTransient<GitHubIssuePluginDefinition>()
        .AddTransient<AzureSupportCenterPluginDefinition>()
        .AddTransient<CpuAnalysisPluginDefinition>()
        .AddTransient<AppCodeAnalysisPluginDefinition>()
        .AddTransient<DotnetAnalysisPluginDefinition>()
        .AddTransient<RoleAssignmentPluginDefinition>()
        .AddTransient<IncidentPluginDefinition>()
        .AddTransient<FunctionAppExecutionFailuresPluginDefinition>()
        .AddTransient<FunctionAppConfigurationChecksPluginDefinition>()
        .AddTransient<UserInteractionPluginDefinition>()
        .AddTransient<DiagnosticsPluginDefinition>()

        .AddTransient<IMetaAgentContainerAppsRemediationPlugin, ContainerAppsRemediationPlugin>()
        .AddTransient<IMetaAgentManagedIdentityMigrationPlugin, ManagedIdentityMigrationPlugin>()
        .AddTransient<IMetaAgentTlsBestPracticesPlugin, TlsBestPracticesPlugin>()
        .AddTransient<IMetaAgentKubernetesAgentPlugin, KubernetesAgentPlugin>()
        .AddTransient<IMetaAgentAksQaAgentPlugin, AksQaAgentPlugin>()
        .AddTransient<IMetaAgentWebAppDownPlugin, WebAppDownPlugin>()
        .AddTransient<IMetaAgentCPUAnalysisPlugin, CPUAnalysisAgentPlugin>()
        .AddTransient<IMetaAgentAppCodeAnalysisPlugin, AppCodeAnalysisAgentPlugin>()
        .AddTransient<IKubePlugin, KubePlugin>()
        //.AddTransient<IMetaAgentAppServiceRemediationPlugin, AppServiceRemediationPlugin>()
        .AddTransient<IChartPlugin, ChartPluginV2>()
        .AddTransient<ChartPluginV2>()
        .AddTransient<IGraphDBPlugin, GraphDBPlugin>()
        .AddTransient<IIncidentPlugin, IncidentPlugin>()
        .AddTransient<IFunctionAppExecutionFailuresPlugin, FunctionAppExecutionFailuresPlugin>()
        .AddTransient<IAzureMonitorMetricsPlugin, AzureMonitorMetricsPlugin>()

        //.AddSingleton<AppServiceRemediationAgentFactory>()
        .AddSingleton<KubernetesAgentFactory>()
        .AddSingleton<AksQaAgentFactory>()
        .AddSingleton<ManagedIdentityMigrationAgentFactory>()
        .AddSingleton<TlsBestPracticeAgentFactory>()
        .AddSingleton<TlsBestPracticesScanner>()
        .AddTransient<IMetaAgentLocalAuthPlugin, LocalAuthAgentPlugin>()
        .AddSingleton<WebAppDownAgentFactory>()
        .AddSingleton<CPUAnalysisAgentFactory>()
        .AddSingleton<AppCodeAnalysisAgentFactory>()
        .AddSingleton<SourceCodeScanner>()
        .AddSingleton<CVEScanner>()
        .AddSingleton<FeedbackRCAScanner>()
        .AddSingleton<IAzMonitorAlertService, AzMonitorAlertService>()
        .AddSingleton<ILogQueryService, LogQueryService>()
        .AddSingleton<IAzMonitorAlertInvestigationService, AzMonitorAlertInvestigationService>()
        .AddSingleton<AzMonitorAlertScanner>()
        .AddSingleton<PostToTeamsPluginDefinition>()
        .AddSingleton<DailyReportScanner>()
        .AddSingleton<AppServiceScanner>()
        .AddSingleton<DailyReportSummaryAgentFactory>()
        .AddSingleton<IPostToTeamsPlugin, PostToTeamsPlugin>()
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
        .AddSingleton<ThreadManagementService>()
        .AddSingleton<IAgentInboundCommunicationService, InboundCommunicationService>()
        .AddSingleton<IAgentOutboundCommunicationService, OutboundCommunicationService>()
        .AddSingleton<IApprovalService, ApprovalService>()
        .AddSingleton<IRemoteWriteService, RemoteWriteService>()
        .AddSingleton<IMetricsRegistry, MetricsRegistry>()
        .AddSingleton<IGremlinMetricsService, GremlinMetricsService>()
        .AddSingleton<AppInsightsPlugin>()
        .AddTransient<ICpuAnalysisPlugin, CpuAnalysisPlugin>()
        .AddTransient<IAppCodeAnalysisPlugin, AppCodeAnalysisPlugin>()
        .AddSingleton<IDotnetAnalysisPlugin, DotnetAnalysisPlugin>()
        .AddSingleton<IReasoningLoopFactory, ReasoningLoopFactory>()
        .AddSingleton<IReasoningLoopManager, ReasoningLoopManager>()
        .AddSingleton<IDiagnosticsPlugin, DiagnosticsPlugin>()

        .AddSingleton<IToolFactory, ToolFactory>(sp =>
        {
            return new ToolFactory(
                logger: sp.GetRequiredService<ILogger<ToolFactory>>(),
                serviceProvider: sp,
                assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.") == true));
        })

        .AddSingleton<IAgentFactory<AgentContext>, AgentFactory<AgentContext>>(sp =>
        {
            return new AgentFactory<AgentContext>(
                logger: sp.GetRequiredService<ILogger<AgentFactory<AgentContext>>>(),
                toolFactory: sp.GetRequiredService<IToolFactory>(),
                assembliesToScan: AppDomain.CurrentDomain.GetAssemblies()
                    .Where(assembly => !assembly.IsDynamic && !string.IsNullOrEmpty(assembly.Location))
                    .Where(assembly => assembly.GetName()?.Name?.StartsWith("Agent.") == true),
                agentsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "AgentsV2"),
                commonPromptsYamlDirectory: Path.Combine(AppContext.BaseDirectory, "CommonPrompts")
            );
        })

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
        .AddSingleton<IResourceMetricsCollector, RedisMetricsCollector>()
        .AddSingleton<IResourceMetricsCollector, AKSMetricsCollector>()

        // helper agents
        .AddTransient<HelperAgentsPluginDefinition>()
        .AddTransient<DiagnosisAgent>()

        // scanner agents
        .AddTransient<CVEAgent>()
        .AddTransient<SourceCodeAgent>()
        ;

    if (isFirstAgent)
    {
        builder.Services.AddSingleton<IAgentsFactory, FirstPartyAgentsFactory>();
        builder.Services.AddSingleton<IToolsRepository, FirstPartyToolsRepository>();
        builder.RegisterFirstPartySubAgentsDependencies();
    }
    else
    {
        builder.Services.AddSingleton<IAgentsFactory, ThirdPartyAgentsFactory>();
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
    builder.Services.AddSingleton<IPagerDutyService, PagerDutyService>();
    builder.Services.AddSingleton<PagerDutyScanner>();

    // Register HttpClientService and configure HttpClient with proper BaseAddress
    builder.Services.AddSingleton<HttpClientService>();
    builder.Services.AddArmHelperHttpClient();
    builder.Services.AddRazorHttpClient();
    builder.Services.AddCrawlerHttpClient();
    builder.Services.AddSingleton<ILogAnalyticsService, LogAnalyticsService>();
    builder.Services.AddSingleton<ILogAnalysisService, LogAnalysisService>();

    // Configure chat services
    builder.Services.ConfigureIChatCompletionService()
                   .ConfigureAzureOpenAIClient()
                   .ConfigureIChatClient()
                   .ConfigureIEmbeddingGenerator();


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
            if (isFirstAgent)
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

    ConfigureLogger();
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
app.MapFallbackToFile("/static/index.html");

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
    var internalKustoClusterSettings = new KustoClusterConfiguration
    {
        ClusterUri = GetInternalKustoClusterConfiguration("ClusterUri"),
        DatabaseName = GetInternalKustoClusterConfiguration("DatabaseName"),
        TableName = GetInternalKustoClusterConfiguration("TableName"),
        Identity = GetInternalKustoClusterConfiguration("Identity")
    };

    var externalKustoClusterUri = GetKustoClusterConfiguration("ClusterUri");
    var externalKustoClusterDatabaseName = GetKustoClusterConfiguration("DatabaseName");
    var externalKustoClusterTableName = GetKustoClusterConfiguration("TableName");
    var externalKustoClusterIdentity = GetKustoClusterConfiguration("Identity");
    var externalKustoClusterSettings = (!string.IsNullOrEmpty(externalKustoClusterUri)
        && !string.IsNullOrEmpty(externalKustoClusterDatabaseName)
        && !string.IsNullOrEmpty(externalKustoClusterTableName)
        && !string.IsNullOrEmpty(externalKustoClusterIdentity))
        ? new KustoClusterConfiguration
        {
            ClusterUri = GetKustoClusterConfiguration("ClusterUri"),
            DatabaseName = GetKustoClusterConfiguration("DatabaseName"),
            TableName = GetKustoClusterConfiguration("TableName"),
            Identity = GetKustoClusterConfiguration("Identity")
        }
        : null;

    builder.Logging.ClearProviders();

    if (builder.Environment.IsDevelopment())
    {
        builder.Logging.AddConsole();
        builder.Services.AddSingleton<AzureDataExplorerLogger>(new AzureDataExplorerLogger());
    }
    else
    {
        if (string.IsNullOrEmpty(internalKustoClusterSettings.ClusterUri) && string.IsNullOrEmpty(externalKustoClusterUri))
        {
            builder.Logging.AddConsole();
        }
        else
        {
            CommonColumn commonColumn = CommonColumn.Build();

            var clientId = GetKustoFirstPartyConfiguration("ClientId");
            var tenantId = "33e01921-4d64-4f8c-a055-5bdaffd5e33d"; // TODO: switch to this when tenant Id is correctly set GetKustoFirstPartyConfiguration("TenantId");
            var certificatePath = GetKustoFirstPartyConfiguration("CertificatePath");

            var logger = new AzureDataExplorerLoggerProvider(
                commonColumn: commonColumn,
                internalKustoClusterUri: internalKustoClusterSettings.ClusterUri,
                internalKustoDatabaseName: internalKustoClusterSettings.DatabaseName,
                internalKustoTableName: internalKustoClusterSettings.TableName,
                externalKustoClusterUri: externalKustoClusterSettings?.ClusterUri,
                externalKustoDatabaseName: externalKustoClusterSettings?.DatabaseName,
                externalKustoTableName: externalKustoClusterSettings?.TableName,
                externalKustoIdentityClientId: externalKustoClusterSettings?.Identity,
                kustoFirstPartyAppClientId: clientId,
                kustoFirstPartyAppTenantId: tenantId,
                kustoFirstPartyAppCertificatePath: certificatePath);

            builder.Services.AddSingleton<ILoggerProvider>(logger);
            builder.Services.AddSingleton<AzureDataExplorerLogger>(logger.GetLogger());
        }
    }
}

string GetKustoFirstPartyConfiguration(string key)
{
    const string prefix = "AppSettings__Core__Azure__Kusto__";
    return Environment.GetEnvironmentVariable($"{prefix}{key}") ?? string.Empty;
}

string GetKustoClusterConfiguration(string key)
{
    const string prefix = "AppSettings__Core__Azure__FirstParty__KustoClusterConfiguration_";
    return Environment.GetEnvironmentVariable($"{prefix}{key}") ?? string.Empty;
}

string GetInternalKustoClusterConfiguration(string key)
{
    const string prefix = "AppSettings__Core__KustoClusterConfiguration_";
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
