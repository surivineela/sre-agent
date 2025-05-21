// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Implementation;

public class ManagedClusterPlugin : IManagedClusterPlugin
{
    private readonly IKustoPluginChat _kustoPlugin;
    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

    public ManagedClusterPlugin(IKustoPluginChat kustoPlugin, IAgentOutboundCommunicationService agentOutboundCommunicationService)
    {
        _kustoPlugin = kustoPlugin;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
    }

    public async Task<string> GetASIPageForManagedCLuster(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
    {
        var clusterName = await _kustoPlugin.ExecuteFunctionAsync("GetManagedClusterName", region,
          new Dictionary<string, string> {
            { "containerAppNameParam", containerAppName },
            { "resourceGroupParam", resourceGroupName },
            { "subscriptionParam", subscriptionId }
          });

    
        var basePath = "/services/ACA Azure Container Apps/pages/Managed Cluster";
        var cleanPath = Uri.EscapeUriString(basePath); // encodes spaces etc.

        var query = $"managedClusterName={Uri.EscapeDataString(clusterName.Result.Trim())}" +
                    $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("M/d/yyyy hh:mm:ss tt"))}" +
                    $"&globalTo={Uri.EscapeDataString(toDate.ToString("M/d/yyyy hh:mm:ss tt"))}";

        var adxUri = $"https://asi.azure.ms{cleanPath}?{query}";

        return $"ASI Page for managed cluster {adxUri}";
    }

    public async Task<string> GetAksClusterCcpNamespace(string region, DateTime fromDate, DateTime toDate, string resourceGroupName, string subscriptionId, string managedClusterName)
    {
        return await _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetAksClusterCcpNamespace", "akshuba.centralus", "AKSprod",
          new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId },
            { "managedClusterName", managedClusterName },
          });
    }
}
