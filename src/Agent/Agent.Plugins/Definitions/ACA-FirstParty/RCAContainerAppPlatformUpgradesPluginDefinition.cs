// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]

    public class RCAContainerAppPlatformUpgradesPluginDefinition
    {
        private readonly IKustoPlugin _kustoPlugin;

        public RCAContainerAppPlatformUpgradesPluginDefinition(IKustoPlugin kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description("""
        Purpose:    
        Get the times for k4apps helm chart upgrades for a given managed cluster.

        Scenario:
        Use this tool to check for K4apps Helm chart upgrade activities that could cause container app revision crashes or impact availability, provisioning, scaling, or health checks.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - StartTime: The time when K4apps helm chart upgrade started
        """)]
            
        public Task<string> GetK4appsHelmChartUpgradeTimes(
            [Description("The start date for the query")] string fromDate,
            [Description("The end date for the query")] string toDate,
            [Description("The region of the managed cluster")] AzureRegion region,
            [Description("The name of the managed cluster")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetK4appsHelmChartUpgradeTimes", region,
             new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "region", region.ToNormalizedString() },
                { "managedClusterName", managedClusterName }
             });
        }

        [Description("""
        Purpose:    
        Get the times for AKS Node Image Upgrades for a specific managed cluster.

        Scenario:
        Use this tool to check for AKS node image upgrade activities that could cause container app revision crashes or impact availability, provisioning, scaling, or health checks.

        Output:
        Returns tab-separated table data in CSV format. Column headers include:
        - StartTime: The time when AKS node image upgrade started
        - EndTime: The time when AKS node image upgrade completed
        - SubOperationName: Operation name for the AKS node image upgrade
        """)]
        public Task<string> GetAksNodeImageUpgradeTimes(
            [Description("The start date for the query")] string fromDate,
            [Description("The end date for the query")] string toDate,
            [Description("The region of the managed cluster")] AzureRegion region,
            [Description("The name of the managed cluster")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetAksNodeImageUpgradeTimes", AzureRegion.CentralUS,
                new Dictionary<string, string> {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "region", region.ToNormalizedString() }
                },
                groupName: "AKS");
        }

        [Description("""
        Purpose:    
        Get the times for Legion Host Role OS upgrades related to a specific container app revision.

        Scenario:
        Use this tool to check for Legion Host Role OS upgrade activities that could cause container app revision crashes or impact availability, provisioning, scaling, or health checks.
    
        Output:
        Returns tab-separated table data in CSV format. Column headers include:
        - PreciseTimeStamp: The time when Legion Host Role OS is upgraded
        """)]
        public async Task<string> GetLegionHostRoleOSUpgradeTimes(
            [Description("The start date for the query")] string fromDate,
            [Description("The end date for the query")] string toDate,
            [Description("The region of the managed cluster")] AzureRegion region,
            [Description("The name of the managed cluster")] string managedClusterName,
            [Description("The name of the container app revision")] string revisionName)
        {
            var cappPodNameQueryResults = await _kustoPlugin.ExecuteLocalFunctionAsync("GetRevisionPodNames", region,
             new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "region", region.ToNormalizedString() },
                { "managedClusterName", managedClusterName },
                { "revisionName", revisionName },
             });

            //var podNamesArray = cappPodNameQueryResults.Split(",").Select(p => p.Substring(p.IndexOf("\""), p.LastIndexOf("\"") + 1)).ToArray().ToString();
            string podNamesArray;
            // Find the position of the first newline which separates the header from the JSON array
            int firstNewLineIndex = cappPodNameQueryResults.IndexOf('\n');
            try
            {
                string jsonPart = cappPodNameQueryResults.Substring(firstNewLineIndex + 1).Trim();
                var podNames = System.Text.Json.JsonSerializer.Deserialize<string[]>(jsonPart);
                podNamesArray = System.Text.Json.JsonSerializer.Serialize(podNames);
            }
            catch (Exception ex)
            {
                return $"failed to get Legion Host Role OS Upgrade Times because {ex.Message}";
            }   

            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetLegionHostRoleOSUpgradeTimes", region, new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "region", region.ToNormalizedString() },
                { "cappPodNames", podNamesArray },
                { "managedClusterName", managedClusterName },
             }, groupName: "Legion");
        }
    }
}
