using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Plugins.Mocks;
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
    public MockSearchEndpointService SearchEndpointService { get; set; }

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
        SearchEndpointService = new MockSearchEndpointService();
    }

}
