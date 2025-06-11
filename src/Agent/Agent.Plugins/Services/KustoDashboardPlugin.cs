// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Data.DatabaseClients.GraphDbClient;
using Agent.Plugins.Kusto;
using Agent.Plugins.Services.Interfaces;

namespace Agent.Plugins.Services
{
    public class KustoDashboardPlugin : IKustoDashboardPlugin
    {
        private readonly IReadOnlyCollection<KustoCluster> _regionalKustoClusters;

        public KustoDashboardPlugin(KustoSettings kustoSettings)
        {
            _regionalKustoClusters = kustoSettings.RegionalClusterGroups.Single(x => string.Equals(x.Name, "ContainerApps", StringComparison.OrdinalIgnoreCase)).Regions;
        }

        public string GenerateDashboardLink(string dashboardId, string startTime, string endTime, string region, string subscriptionId, string resourceGroupName, string managedClusterName, string containerAppName, string revisionName)
        {
            region = region.NormalizeLocation();
            var startTimeParam = $"p-_startTime={startTime}";
            var endTimeParam = $"p-_endTime={endTime}";
            var cluster = _regionalKustoClusters.Where(KustoCluster => { return KustoCluster.Region == region.NormalizeLocation(); }).FirstOrDefault();
            var clusterUriParam = $"p-ClusterUri={cluster.ClusterUri}";
            var subscriptionIdParam = $"p-subscriptionId={subscriptionId}";
            var resourceGroupNameParam = $"p-resourceGroupName={resourceGroupName}";
            var managedClusterNameParam = $"p-managedClusterName={managedClusterName}";
            var containerAppNameParam = $"p-containerAppName={containerAppName}";
            var revisionNameParam = $"p-revisionName={revisionName}";
            var dashboardLink = $"https://dataexplorer.azure.com/dashboards/{dashboardId}?{startTimeParam}&{endTimeParam}&{clusterUriParam}&{subscriptionIdParam}&{resourceGroupNameParam}&{managedClusterNameParam}&{containerAppNameParam}&{revisionNameParam}";
            return dashboardLink;
        }
    }
}
