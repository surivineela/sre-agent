using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppResourceSearchPluginDefinition
    {
        private readonly IKustoPlugin _kustoPlugin;

        public RCAContainerAppResourceSearchPluginDefinition(IKustoPlugin kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description("""
        Purpose:
        Searches ContainerApp,ContainerAppsJob,ManagedEnvironment,ManagedCluster,SessionPool resources by the given resource name.

        Scenario:
        Use this tool to search resources that match the specified name. Verify that the resource name is correct and exists.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - ResourceType: Type of the resource (ContainerApp,ContainerAppsJob,ManagedEnvironment,ManagedCluster,SessionPool)
        - region
        - subscription: subscriptionId of the resource
        - managedClusterName
        - IsMultiTenantCluster: Indicates if the managed cluster is multi-tenant
        - managedEnvironmentName
        - containerAppName
        - containerAppsJobName
        - IsRunOnLegion: Indicates if the container app is running on Legion
        - sessionPoolName
        - provisioningState: Provisioning state of the container app
        - containerAppResourceGroup
        - managedEnvironmentResourceGroup
        - sessionPoolResourceGroup
        - environmentProvisioningState: Provisioning state of the managed environment
        """
        )]
        public async Task<string> SearchContainerAppsResourcesByName(
            [Description("Start date for the search range in ISO 8601 format.")] DateTime fromDate,
            [Description("End date for the search range in ISO 8601 format.")] DateTime toDate,
            [Description("Name of the resource to search for.")] string resourceName,
            [Description("Azure region of the resource to search for.")] string region,
            [Description("Subscription ID")] string subscriptionId)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("SearchResourceByName", "eastus",
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "resourceName", resourceName },
                    { "region", region },
                    { "subscriptionId", subscriptionId }
                });
        }
    }
}
