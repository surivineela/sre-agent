// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace FirstPartyAgent.Plugins
{
    /// <summary>
    /// Generate Azure Data Explorer Dashboard Links for Container Apps Investigations
    /// </summary>
    public interface IPlatformUpgradesPlugin
    {
        public Task<string> GetK4appsHelmChartUpgradeTimes(string fromDate, string toDate, string region, string managedClusterName);

        public Task<string> GetAksNodeImageUpgradeTimes(string fromDate, string toDate, string region, string managedClusterName);

        public Task<string> GetLegionHostRoleOSUpgradeTimes(string fromDate, string toDate, string region, string managedClusterName, string revisionName);

    }
}
