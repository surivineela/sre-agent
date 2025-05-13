// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Implementation;

public class NodeAvailabilityPlugin : INodeAvailabilityPlugin
{
    private readonly IKustoPluginChat _kustoPlugin;

    public NodeAvailabilityPlugin(IKustoPluginChat kustoPlugin)
    {
        _kustoPlugin = kustoPlugin;
    }

    public async Task<string> GetNodeAvailabilityFailures(string region, DateTime fromDate, DateTime toDate, string containerAppName, string revisionName)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetNodeAvailabilityFailures", region,
      new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "containerAppName", containerAppName },
            { "revisionName", revisionName }
          }
      );
    }

}

