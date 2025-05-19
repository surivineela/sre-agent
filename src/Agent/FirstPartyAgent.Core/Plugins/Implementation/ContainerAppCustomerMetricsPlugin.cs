using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Implementation
{
    public class ContainerAppCustomerMetricsPlugin : IContainerAppCustomerMetricsPlugin
    {
        [KernelFunction(KernelFunctionNames.ACA.GetMetricsMdmCount)]
        public Task<string> GetMetricsMdmCount()
        {
            return Task.FromResult("There are no missing metrics");
        }
    }
}
