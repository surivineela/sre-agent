using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Plugins.Definitions;
using Agent.Plugins;
using Agent.Plugins.Mocks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Time.Testing;

namespace Agent.Tests.Common.Mocks;
public class BasicMockSetup
{
    public TimeProvider TimeProvider { get; set; }
    public MockRecordActionsPlugin RecordActionsPlugin { get; set; }
    public MockArmPlugin ArmPlugin { get; set; }
    public MockMetricsPlugin MetricsPlugin { get; set; }
    public MockTimePlugin TimePlugin { get; set; }
    public MockCommunicationService CommunicationService { get; set; }
    public MockContainerAppPlugin ContainerAppPlugin { get; set; }
    public MockNSGRulePlugin NSGRulePlugin { get; set; }

    public MockGrafanaPlugin GrafanaPlugin { get; set; }
    public MockGraphDBPlugin GraphDBPlugin { get; set; }

    public ILogger? Logger { get; set; }

    public BasicMockSetup(DateTimeOffset mockedCurrentDateTime, ILogger? logger)
    {
        Logger = logger;

        TimeProvider = new FakeTimeProvider(mockedCurrentDateTime);
        ArmPlugin = new MockArmPlugin(TimeProvider);

        MetricsPlugin = new MockMetricsPlugin(TimeProvider);
        TimePlugin = new MockTimePlugin(TimeProvider);
        CommunicationService = new MockCommunicationService(logger: logger);
        RecordActionsPlugin = new MockRecordActionsPlugin(TimeProvider, logger: logger);

        GrafanaPlugin = new MockGrafanaPlugin();
        GraphDBPlugin = new MockGraphDBPlugin();

        NSGRulePlugin = new MockNSGRulePlugin();
        ContainerAppPlugin = new MockContainerAppPlugin(NSGRulePlugin);
    }

}

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
}
