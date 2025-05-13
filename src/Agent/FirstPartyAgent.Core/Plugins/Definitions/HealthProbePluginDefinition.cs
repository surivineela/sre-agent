// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    // These are tools exposed to any-sub agent that uses this plugin but mostly it will be used by 'RevisionAgent'
    // Note!!: If this plugin is used by other agent, then we are mixing the concerns and we need to refactor this plugin
    public class HealthProbePluginDefinition
    {
        private readonly IHealthProbePlugin _plugin;

        public HealthProbePluginDefinition(IHealthProbePlugin Plugin)
        {
            _plugin = Plugin;
        }



        [KernelFunction(KernelFunctionNames.ACA.GetHealthProbeFailures)]
        [Description(
@"Retrieve readiness/liveness/startup probe failures for a specific Azure container app revision.

Projects:
- msg: Log message of the probe failure.
- count: Number of times the probe failed with the same message consecutively.
- replicaName: Name of the replica where the failure occurred.
- revisionName: Name of the container app revision.
- level: Severity level of the failure (e.g., error, warning)."
)]
        public Task<string> GetHealthProbeFailures(
    [Description("Azure region.")] string region,
    [Description("Start time of the query.")] DateTime fromDate,
    [Description("End time of the query.")] DateTime toDate,
    [Description("Name of the container app.")] string containerAppName,
    [Description("Name of the revision.")] string revisionName,
    [Description("provide sampling inputs")] SamplingOptions sampling)
        {
            return _plugin.GetHealthProbeFailures(region.NormalizeLocation(), fromDate, toDate, containerAppName, revisionName);
        }
    }
}
