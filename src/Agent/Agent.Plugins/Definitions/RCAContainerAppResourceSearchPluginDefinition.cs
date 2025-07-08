using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppResourceSearchPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public RCAContainerAppResourceSearchPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description("""
        Searches ContainerApp, ContainerAppsJob, ManagedEnvironment, ManagedCluster, SessionPool resources by the given resource name.
        Use this tool to search resources that match the specified name. Verify that the resource name is correct and exists.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        Tool Output:
        - ResourceType: Type of the resource (ContainerApp, ContainerAppsJob, ManagedEnvironment, ManagedCluster, SessionPool)
        - region: region of the resource
        - subscription: subscriptionId of the resource
        - managedClusterName: 
        - managedEnvironmentName: 
        - containerAppName: 
        - containerAppsJobName: 
        - provisioningState: Provisioning state of the container app
        - sessionPoolName: 
        - containerAppResourceGroup: ContainerApp resource group
        - managedEnvironmentResourceGroup: ManagedEnvironment resource group
        - environmentProvisioningState: Provisioning state of the managed environment
        - sessionPoolResourceGroup: SessionPool resource group
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
