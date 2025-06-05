using System.ComponentModel;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Plugins;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class PlatformUpgradesPluginDefinition(IPlatformUpgradesPlugin platformUpgradePlugin)
    {
        private readonly IPlatformUpgradesPlugin _platformUpgradePlugin = platformUpgradePlugin;

        [KernelFunction(KernelFunctionNames.ACA.GetK4appsHelmChartUpgradeTimes)]
        [Description(@"Get the times of K4apps Helm chart upgrades within a specified date range and region for a managed cluster.")]
        public Task<string> GetK4appsHelmChartUpgradeTimes(string fromDate, string toDate, string region, string managedClusterName)
        {
            return _platformUpgradePlugin.GetK4appsHelmChartUpgradeTimes(fromDate, toDate, region, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetAksNodeImageUpgradeTimes)]
        [Description(@"Get the times of AKS node image upgrades within a specified date range, region, and subscription for a managed cluster.")]
        public Task<string> GetAksNodeImageUpgradeTimes(string fromDate, string toDate, string region, string managedClusterName)
        {
            return _platformUpgradePlugin.GetAksNodeImageUpgradeTimes(fromDate, toDate, region, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetLegionHostRoleOSUpgradeTimes)]
        [Description(@"Get the times of Legion host role OS upgrades within a specified date range, region, managed cluster, and revision name.")]
        public Task<string> GetLegionHostRoleOSUpgradeTimes(string fromDate, string toDate, string region, string managedClusterName, string revisionName)
        {
            return _platformUpgradePlugin.GetLegionHostRoleOSUpgradeTimes(fromDate, toDate, region, managedClusterName, revisionName);
        }
    }
}
