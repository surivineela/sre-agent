using System.Net.WebSockets;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins;
using Agent.Plugins.Interface;
using Gremlin.Net.Driver;
using Gremlin.Net.Structure.IO.GraphSON;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

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
        services.AddSingleton<ISearchEndpointService>(mocks.SearchEndpointService);
    }

    public static void AddLocalGremlin(this IServiceCollection services, string graphName)
    {
        try
        {
            var server = new GremlinServer("localhost", 8182);
#pragma warning disable CS0618 // Type or member is obsolete
            var internalClient = new GremlinClient(server, new CustomGraphSON2Reader(), new GraphSON2Writer(), "application/vnd.gremlin-v2.0+json");
#pragma warning restore CS0618 // Type or member is obsolete
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
