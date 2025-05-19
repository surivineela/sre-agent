using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Plugins.Definitions;
using Agent.Plugins;
using Agent.Tests.Common.Mocks;
using Microsoft.Extensions.DependencyInjection;

namespace Agent.Tests.Common.ScenarioTestHelpers;
public static class TlsTestHelpers
{
    public static void AddPluginDefinitionsForTlsSubAgent(this IServiceCollection services)
    {
        services.AddSingleton<MetricsPluginDefinition>();
        services.AddSingleton<ArmPluginDefinition>();
        services.AddSingleton<RecordActionsPluginDefinition>();
        services.AddSingleton<ControlFlowPluginDefinition>();
    }
}
