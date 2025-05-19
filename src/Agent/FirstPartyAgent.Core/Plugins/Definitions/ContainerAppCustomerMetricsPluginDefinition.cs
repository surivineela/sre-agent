using System.ComponentModel;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class ContainerAppCustomerMetricsPluginDefinition(IContainerAppCustomerMetricsPlugin Plugin)
    {
        private readonly IContainerAppCustomerMetricsPlugin _metricsAgentPlugin = Plugin;

        [KernelFunction(KernelFunctionNames.ACA.GetMetricsMdmCount)]
        [Description(@"Get Count aggregation for metrics")]
        public Task<string> GetMetricsMdmCount()
        {
            return _metricsAgentPlugin.GetMetricsMdmCount();
        }
    }

}
