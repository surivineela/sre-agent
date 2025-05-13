// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Implementation;

public class ManagedEnvironmentPlugin : IManagedEnvironmentPlugin
{
    private readonly IKustoPluginChat _kustoPlugin;
    private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

    public ManagedEnvironmentPlugin(IKustoPluginChat kustoPlugin, IAgentOutboundCommunicationService agentOutboundCommunicationService)
    {
        _kustoPlugin = kustoPlugin;
        _agentOutboundCommunicationService = agentOutboundCommunicationService;
    }

    public async Task<string> GetManagedEnvironmentInformation(string region, DateTime fromDate, DateTime toDate, string managedEnvironmentName, string resourceGroupName, string subscriptionId, SamplingOptions? samplingOptions)
    {
        return await _kustoPlugin.ExecuteLocalFunctionAsync("GetManagedEnvironment", region,
      new Dictionary<string, string> {
            { "fromDate", fromDate.ToString() },
            { "toDate", toDate.ToString() },
            { "region", region },
            { "environmentName", managedEnvironmentName },
            { "resourceGroupName", resourceGroupName },
            { "subscriptionId", subscriptionId }
          }
      );
    }

    public async Task<string> GetASIPageForManagedEnvironment(string region, DateTime fromDate, DateTime toDate, string environmentName, string resourceGroupName, string subscriptionId)
    {
        var basePath = "/services/ACA Azure Container Apps/pages/Container App Environment";

        var cleanPath = Uri.EscapeUriString(basePath); // encodes spaces etc.

        var query = $"environmentLocation={Uri.EscapeDataString(region.NormalizeLocation())}" +
           $"&environmentName={Uri.EscapeDataString(environmentName)}" +
           $"&environmentResourceGroup={Uri.EscapeDataString(resourceGroupName)}" +
           $"&environmentSubscription={Uri.EscapeDataString(subscriptionId)}" +
           $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("M/d/yyyy hh:mm:ss tt"))}" +
           $"&globalTo={Uri.EscapeDataString(toDate.ToString("M/d/yyyy hh:mm:ss tt"))}";

        var adxUri = $"https://asi.azure.ms{cleanPath}?{query}";

        return $"ASI Page for managed environment {adxUri}";
    }
}

