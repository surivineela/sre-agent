using Azure.AI.OpenAI;
using Azure.Identity;
using Azure.Monitor.OpenTelemetry.Exporter;
using Microsoft.Azure.Functions.Worker.Builder;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using OperationalAgentCore;

var builder = FunctionsApplication.CreateBuilder(args);

builder.ConfigureFunctionsWebApplication();

var config = builder.Configuration;

config.SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true) //load base settings
            .AddJsonFile("appsettings.development.json", optional: true, reloadOnChange: true) //load local settings
            .AddEnvironmentVariables();

var azureSettings = config.GetSection("Azure").Get<AzureSettings>();
if (azureSettings == null)
{
    throw new NullReferenceException("Azure settings are required.");
}

AppSettings? appSettings = config.GetSection("AppSettings").Get<AppSettings>();

AppContext.SetSwitch("Microsoft.SemanticKernel.Experimental.GenAI.EnableOTelDiagnosticsSensitive", true);
ResourceBuilder resourceBuilder = GetResourceBuilder();
using TracerProvider tracerProvider = GetTracerProvider(resourceBuilder, azureSettings, appSettings);
using MeterProvider meterProvider = GetMeterProvider(resourceBuilder, azureSettings);

// Logs seem more useful if we don't send everything to App Insights
//using ILoggerFactory loggerFactory = GetLoggerFactory(resourceBuilder, azureSettings);
//builder.Services.AddSingleton(loggerFactory);

SemanticKernelHelper.ConfigService(builder.Services);

builder.Services.AddSingleton<TeamsConnector>();
builder.Services.AddHttpClient();

// Application Insights isn't enabled by default. See https://aka.ms/AAt8mw4.
// builder.Services
//     .AddApplicationInsightsTelemetryWorkerService()
//     .ConfigureFunctionsApplicationInsights();

builder.Services.AddSingleton<AzureOpenAIClient>(GetAzureOpenAIClient(azureSettings));
builder.Services.AddSingleton<IChatClient>(serviceProvider => GetChatClient(serviceProvider.GetRequiredService<AzureOpenAIClient>(), azureSettings));

builder.Build().Run();


AzureOpenAIClient GetAzureOpenAIClient(AzureSettings azureSettings)
{
    string? aoaiEndpoint = azureSettings.OpenAI.Endpoint;
    string key = azureSettings.OpenAI.ApiKey;

    if (string.IsNullOrEmpty(aoaiEndpoint))
        throw new Exception("Please set `OpenAI` settings in the appsettings.development.json, check the readme for more information.");

    Console.WriteLine($" * Using Azure OpenAI endpoint (AzureOpenAIEndpoint): {aoaiEndpoint}");

    if (string.IsNullOrEmpty(azureSettings.OpenAI.ApiKey))
    {
        Console.WriteLine("No OpenAIAPI_KEY found, using DefaultAzureCredential");
        return new AzureOpenAIClient(new Uri(aoaiEndpoint), new DefaultAzureCredential());
    }
    else
    {
        return new AzureOpenAIClient(new Uri(aoaiEndpoint), new System.ClientModel.ApiKeyCredential(key));
    }
}

IChatClient GetChatClient(AzureOpenAIClient client, AzureSettings azureSettings)
{
    string? deployment = azureSettings.OpenAI.DeploymentName;

    if (string.IsNullOrEmpty(deployment))
        throw new Exception("Please set `AzureOpenAIDeployment`, check the readme for more information.");

    return new ChatClientBuilder(client.AsChatClient(deployment))
        // disable this so that we can control the dispatch
        //.UseFunctionInvocation() 
        .Build();
}

ResourceBuilder GetResourceBuilder()
{
    return ResourceBuilder
        .CreateDefault()
        .AddService("OperationsAgentSK");
}

TracerProvider GetTracerProvider(ResourceBuilder resourceBuilder, AzureSettings azureSettings, AppSettings? appSettings)
{
    TracerProviderBuilder builder = Sdk.CreateTracerProviderBuilder()
        .SetResourceBuilder(resourceBuilder)
        .AddSource("Microsoft.SemanticKernel*");

    if (appSettings?.LogGenAICalls == true)
    {
        builder = builder.AddConsoleExporter();
    }

    if (!string.IsNullOrEmpty(azureSettings.AppInsightsConnectionString))
    {
        builder = builder.AddAzureMonitorTraceExporter(options => options.ConnectionString = azureSettings.AppInsightsConnectionString);
    }

    return builder.Build();
}

MeterProvider GetMeterProvider(ResourceBuilder resourceBuilder, AzureSettings azureSettings)
{
    MeterProviderBuilder builder = Sdk.CreateMeterProviderBuilder()
        .SetResourceBuilder(resourceBuilder)
        .AddMeter("Microsoft.SemanticKernel*");

    if (!string.IsNullOrEmpty(azureSettings.AppInsightsConnectionString))
    {
        builder = builder.AddAzureMonitorMetricExporter(options => options.ConnectionString = azureSettings.AppInsightsConnectionString);
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
            if (!string.IsNullOrEmpty(azureSettings.AppInsightsConnectionString))
            {
                options.AddAzureMonitorLogExporter(options => options.ConnectionString = azureSettings.AppInsightsConnectionString);
            }
            // Format log messages. This is default to false.
            options.IncludeFormattedMessage = true;
            options.IncludeScopes = true;
        });
    });
}