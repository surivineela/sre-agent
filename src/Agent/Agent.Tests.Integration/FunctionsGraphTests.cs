using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Tests.Common.Mocks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Agent.Tests.Integration;
public class FunctionsGraphTests
{
    private ILogger _logger;
    private IHost _host;
    private IGraphDatabaseClient _graphClient;

    public FunctionsGraphTests(ITestOutputHelper testOutputHelper)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { EnvironmentName = Environments.Development });
        builder.Services.AddLocalGremlin("gfunctionsBadFlexApps");

        _logger = testOutputHelper.ToLogger<ILogger>();
        _host = builder.Build();
        _graphClient = _host.Services.GetRequiredService<IGraphDatabaseClient>();
    }

    [Fact]
    public async Task TestConnectDB()
    {
        var result = await _graphClient.Query("g.V().has('isDeleted', false).toList()");
        Assert.True(result.Count > 0, "No vertices found");
    }

    [Fact]
    public async Task ListFunctionApps()
    {
        var loggerFactory = _host.Services.GetRequiredService<ILoggerFactory>();
        var plugin = new FunctionAppsPlugin(_graphClient, loggerFactory.CreateLogger<FunctionAppsPlugin>());
        var definition = new FunctionAppsPluginDefinition(plugin);

        var apps = await definition.ListFunctionAppsAsync(new Guid("29e3378b-0aaf-45da-b3c6-6fd0eea164e4"));

        foreach(var app in apps)
        {
            _logger.LogInformation($"Function App: {app.Name}, Resource ID: {app.ResourceId}");
        }

        Assert.True(apps.Count == 14, "No apps found");
    }
}
