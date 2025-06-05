using System.ComponentModel;
using Agent.Core.Interfaces;
using Agent.Data.DatabaseClients.GraphDbClient;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Interfaces;
using FirstPartyAgent.Plugins;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class PlatformUpgradePlugin: IPlatformUpgradesPlugin
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public PlatformUpgradePlugin(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }
        public Task<string> GetK4appsHelmChartUpgradeTimes(string fromDate, string toDate, string region, string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetK4appsHelmChartUpgradeTimes", region,
             new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "region", region },
                { "managedClusterName", managedClusterName }
             });
        }

        public Task<string> GetAksNodeImageUpgradeTimes(string fromDate, string toDate, string region, string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetAksNodeImageUpgradeTimes", "akshuba.centralus", "AKSprod",
                new Dictionary<string, string> {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "region", region }
                });
        }

        public async Task<string> GetLegionHostRoleOSUpgradeTimes(string fromDate, string toDate, string region, string managedClusterName, string revisionName)
        {
            var cappPodNameQueryResults = await _kustoPlugin.ExecuteLocalFunctionAsync("GetRevisionPodNames", region,
             new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "region", region },
                { "managedClusterName", managedClusterName },
                { "revisionName", revisionName },
             });

            //var podNamesArray = cappPodNameQueryResults.Split(",").Select(p => p.Substring(p.IndexOf("\""), p.LastIndexOf("\"") + 1)).ToArray().ToString();
            var podNames = System.Text.Json.JsonSerializer.Deserialize<string[]>(cappPodNameQueryResults);
            var podNamesArray = System.Text.Json.JsonSerializer.Serialize(podNames);

            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetLegionHostRoleOSUpgradeTimes", region, new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "region", region },
                { "cappPodNames", podNamesArray },
                { "managedClusterName", managedClusterName },
             }, groupName: "Legion");
        }
    }
}
