using Agent.Core.Interfaces;
using Agent.Plugins.Definitions;
using Agent.Plugins;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Agent.Data.DatabaseClients.GraphDbClient;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Agent.Plugins.Implementation;
using Agent.Runtime.SubAgents;
using Moq;
using Agent.Core.Helpers;
using System.Net.Sockets;
using System.Net.WebSockets;
using Agent.Runtime.Communication;
using Agent.Runtime.MetaAgent;
using Agent.Runtime.Services;

namespace Agent.Tests.Common.Mocks;

public static class ServiceCollectionExtensionsForMocks
{
    public static void AddMockServices(this IServiceCollection services, BasicMockSetup mocks)
    {
        services.AddSingleton<TimeProvider>(mocks.TimeProvider);
        services.AddSingleton<IRecordActionsPlugin>(mocks.RecordActionsPlugin);
        services.AddSingleton<IArmPlugin>(mocks.ArmPlugin);
        services.AddSingleton<IMetricsPlugin>(mocks.MetricsPlugin);
        services.AddSingleton<ITimePlugin>(mocks.TimePlugin);
        services.AddSingleton<IAgentOutboundCommunicationService>(mocks.CommunicationService)
                .AddSingleton<IChartPlugin>(new ChartPlugin(null, mocks.CommunicationService));


        services.AddSingleton<IGrafanaPlugin>(mocks.GrafanaPlugin);
        services.AddSingleton<IGraphDBPlugin>(mocks.GraphDBPlugin);

        services.AddSingleton<INSGRulePlugin>(mocks.NSGRulePlugin);
        services.AddSingleton<IContainerAppPlugin>(mocks.ContainerAppPlugin);
    }

    public static void AddMockServices(this IServiceCollection services, MetaAgentMockSetup mocks)
    {
        services.AddLocalGremlin(mocks.GraphName);

        services.AddSingleton<IAgentOutboundCommunicationService>(sp =>
        {
            var logger = sp.GetRequiredService<ILoggerFactory>()
                .CreateLogger<IAgentOutboundCommunicationService>();
            return new MockCommunicationService(logger);
        });

        services.AddSingleton<IFunctionAppsPlugin, FunctionAppsPlugin>();
        services.AddSingleton<IAppServicePlugin, AppServicePlugin>();

    }

    public static void AddServices(this IServiceCollection services, E2EMockSetup mocks)
    {
        services.AddMockServices(mocks.BasicMocks);
        services.AddLocalGremlin(mocks.GraphName);

        // Overwrite the graph mock setup in basic mocks with a real one (it will use the gremlin container)
        services.AddSingleton<GraphDBPlugin>();
        services.AddSingleton<IGraphDBPlugin, GraphDBPlugin>();
        services.AddSingleton<GraphDBPluginDefinition>();

        services.AddSingleton<ThreadManagementService>();
        services.AddSingleton<IAgentInboundCommunicationService, InboundCommunicationService>();
        services.AddSingleton<IAgentOutboundCommunicationService, OutboundCommunicationService>();
        services.AddTransient<IAgent, MetaAgent>();
        services.AddSingleton<IAuthenticationService>(mocks.AuthenticationService);
        services.AddSingleton<IToolsRepository, ToolsRepository>();
    }

    public static void AddLocalGremlin(this IServiceCollection services, string graphName)
    {
        try
        {
            var server = new GremlinServer("localhost", 8182);
            var internalClient = new GremlinClient(server, new CustomGraphSON2Reader(), new GraphSON2Writer(), "application/vnd.gremlin-v2.0+json");
            services.AddSingleton<IGraphDatabaseClient>(sp =>
            {
                var gremlinLogger = sp.GetRequiredService<ILoggerFactory>()
                    .CreateLogger<GremlinGraphDatabaseClient>();
                var dbClient = new GremlinGraphDatabaseClient(internalClient, gremlinLogger);
                dbClient.GraphName = graphName;
                return dbClient;
            });
        }
        catch (Exception ex) when (ex.InnerException is WebSocketException || ex.InnerException?.Message == "Unable to connect to the remote server")
        {
            throw new Exception("Could not connect to the gremlin container. Run the container using `run-gremlin-emulator.sh` and verify that the container started successfully using `docker logs gremlin`.", ex);
        }
    }
}
