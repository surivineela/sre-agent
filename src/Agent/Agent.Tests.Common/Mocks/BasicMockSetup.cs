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
    public MockApprovalPlugin ApprovalPlugin { get; set; }
    public MockRecordActionsPlugin RecordActionsPlugin { get; set; }
    public MockArmPlugin ArmPlugin { get; set; }
    public MockMetricsPlugin MetricsPlugin { get; set; }
    public MockTimePlugin TimePlugin { get; set; }
    public MockCommunicationService CommunicationService { get; set; }

    public BasicMockSetup(DateTimeOffset mockedCurrentDateTime, ILogger? logger)
    {
        TimeProvider = new FakeTimeProvider(mockedCurrentDateTime);
        ApprovalPlugin = new MockApprovalPlugin();
        ArmPlugin = new MockArmPlugin(TimeProvider);

        MetricsPlugin = new MockMetricsPlugin(TimeProvider);
        TimePlugin = new MockTimePlugin(TimeProvider);
        CommunicationService = new MockCommunicationService(logger: logger);
        RecordActionsPlugin = new MockRecordActionsPlugin(TimeProvider, logger: logger);
    }

}

public static class ServiceCollectionExtensionsForMocks
{
    public static void AddMockServices(this IServiceCollection services, BasicMockSetup mocks)
    {
        services.AddSingleton<TimeProvider>(mocks.TimeProvider);
        services.AddSingleton<IApprovalPlugin>(mocks.ApprovalPlugin);
        services.AddSingleton<IRecordActionsPlugin>(mocks.RecordActionsPlugin);
        services.AddSingleton<IArmPlugin>(mocks.ArmPlugin);
        services.AddSingleton<IMetricsPlugin>(mocks.MetricsPlugin);
        services.AddSingleton<ITimePlugin>(mocks.TimePlugin);
        services.AddSingleton<IAgentOutboundCommunicationService>(mocks.CommunicationService);
    }
}
