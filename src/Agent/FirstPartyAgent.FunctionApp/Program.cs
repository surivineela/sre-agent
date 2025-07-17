// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation. All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Sinks.AzureDataExplorer;
using Serilog.Sinks.AzureDataExplorer.Extensions;
using FirstPartyAgent.Core.Helpers;
using FirstPartyAgent.Core.Extensions;

// Enable Serilog internal diagnostics (optional, but helps troubleshooting)
Serilog.Debugging.SelfLog.Enable(msg => Console.Error.WriteLine($"SeriLogError: {msg}"));

// Build configuration first so we can read Serilog settings from it.
var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production"}.json", optional: true, reloadOnChange: true)
    .LoadKeyVaultAppSettings()
    .AddEnvironmentVariables()
    .Build();

var kustoClusterUri = config["Serilog:KustoClusterUri"];
var kustoDatabase = config["Serilog:KustoDatabase"];
var kustoTable = config["Serilog:KustoTable"];
var batchPostingLimitString = config["Serilog:BatchPostingLimit"];
int batchPostingLimit = 100;
if (!string.IsNullOrWhiteSpace(batchPostingLimitString) &&
    int.TryParse(batchPostingLimitString, out int limit))
{
    batchPostingLimit = limit;
}

var serilogOptions = new AzureDataExplorerSinkOptions
{
    IngestionEndpointUri = kustoClusterUri,
    DatabaseName = kustoDatabase,
    TableName = kustoTable,
    BatchPostingLimit = batchPostingLimit,
};

var isDevelopment = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("WEBSITE_INSTANCE_ID"));
if (!isDevelopment)
{
    serilogOptions = serilogOptions.WithAadSystemAssignedManagedIdentity();
}

var loggerConfig = new LoggerConfiguration();
if (!isDevelopment)
{
    loggerConfig = loggerConfig.WriteTo.AzureDataExplorerSink(serilogOptions);
}

if (isDevelopment)
{
    LocalAadAuthenticator.Initialize();
}

var logger = loggerConfig.WriteTo.Console().CreateLogger();

var host = new HostBuilder()
    .UseSerilog(logger)
    .ConfigureAppConfiguration((context, configBuilder) =>
    {
        configBuilder.SetBasePath(context.HostingEnvironment.ContentRootPath)
                     .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                     .AddJsonFile($"appsettings.{context.HostingEnvironment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                     .AddEnvironmentVariables();
    })
    .ConfigureFunctionsWebApplication()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        var configuration = context.Configuration;

        services.RegisterServiceDependencies(context.HostingEnvironment);
        services.ConfigureSemanticKernel();
        services.AddSingleton<IChatService, ChatProcessingService>();
    })
    .Build();

host.Run();
