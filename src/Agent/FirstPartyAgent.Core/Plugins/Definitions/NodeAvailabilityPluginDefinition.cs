// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    // These are tools exposed to any-sub agent that uses this plugin but mostly it will be used by 'RevisionAgent'
    // Note!!: If this plugin is used by other agent, then we are mixing the concerns and we need to refactor this plugin
    public class NodeAvailabilityPluginDefinition
    {
        private readonly INodeAvailabilityPlugin _plugin;

        public NodeAvailabilityPluginDefinition(INodeAvailabilityPlugin Plugin)
        {
            _plugin = Plugin;
        }



        [KernelFunction(KernelFunctionNames.ACA.GetNodeAvailabilityFailures)]
        [Description(
@"Retrieve node availability failure events for a specific Azure container app revision.

Projects:
- preciseTimeStamp: Precise timestamp of the event.
- replicaName: Name of the replica where the failure occurred.
- revisionName: Name of the container app revision.
- msg: Log message of the node unavailability."
)]
        public Task<string> GetNodeAvailabilityFailures(
    [Description("Azure region.")] string region,
    [Description("Start time of the query.")] DateTime fromDate,
    [Description("End time of the query.")] DateTime toDate,
    [Description("Name of the container app.")] string containerAppName,
    [Description("Name of the revision.")] string revisionName)
        {
            return _plugin.GetNodeAvailabilityFailures(region.NormalizeLocation(), fromDate, toDate, containerAppName, revisionName);
        }
    }
}
