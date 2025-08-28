using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins.Definitions;
using Agent.Plugins.Implementation;
using Agent.Tests.Common.Mocks;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;
using Moq;
using Agent.Core.Helpers;
using Agent.Core.Interfaces;
using Agent.Core.Models;
using Agent.Core.Services;
using Agent.Core.Configuration;

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
        
        // Create a mock ArmHelper with minimal setup for integration test
        var mockArmLogger = new Mock<ILogger<ArmHelper>>();
        var mockHttpClientFactory = new Mock<IHttpClientFactory>();
        var mockArmClientFactory = new Mock<IArmClientFactory>();
        var mockAuthService = new Mock<IAuthenticationService>();
        var mockHostEnvironment = new Mock<IHostEnvironment>();
        var mockChatClient = new Mock<IChatClient>();
        var mockCrawlerTriggerService = new Mock<ICrawlerTriggerService>();
        var mockSessionPoolService = new Mock<ISessionPoolService>();
        var azureSettings = new AzureSettings();

        var armHelper = new ArmHelper(
            mockArmLogger.Object,
            mockHttpClientFactory.Object,
            mockArmClientFactory.Object,
            mockAuthService.Object,
            azureSettings,
            mockHostEnvironment.Object,
            mockCrawlerTriggerService.Object,
            mockSessionPoolService.Object,
            mockChatClient.Object);

        var plugin = new FunctionAppsPlugin(_graphClient, loggerFactory.CreateLogger<FunctionAppsPlugin>(), armHelper);
        var definition = new FunctionAppsPluginDefinition(plugin);

        var apps = await definition.ListFunctionAppsAsync(new Guid("29e3378b-0aaf-45da-b3c6-6fd0eea164e4"));

        foreach(var app in apps)
        {
            _logger.LogInformation($"Function App: {app.Name}, Resource ID: {app.ResourceId}");
        }

        Assert.True(apps.Count == 14, "No apps found");
    }
}
