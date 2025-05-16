// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Implementation;

public class HealthProbePlugin : IHealthProbePlugin
{
    private readonly IKustoPluginChat _kustoPlugin;

    public HealthProbePlugin(IKustoPluginChat kustoPlugin)
    {
        _kustoPlugin = kustoPlugin;
    }

    public async Task<string> GetHealthProbeFailures(string region, DateTime fromDate, DateTime toDate, string containerAppName, string revisionName)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetHealthProbeFailures", region,
      new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "containerAppName", containerAppName },
            { "revisionName", revisionName }
          }
      );
    }

    public async Task<string> GetHealthProbeSettings(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string containerAppName)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetHealthProbeSettings", region,
      new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "subscriptionId", subscriptionId },
            { "resourceGroupName", resourceGroupName },
            { "containerAppName", containerAppName }
          }
      );
    }

}

