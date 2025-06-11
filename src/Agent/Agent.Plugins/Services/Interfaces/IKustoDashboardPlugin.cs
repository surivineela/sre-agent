// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.Services.Interfaces
{
    /// <summary>
    /// Generate Azure Data Explorer Dashboard Links for Container Apps Investigations
    /// </summary>
    public interface IKustoDashboardPlugin
    {
        string GenerateDashboardLink(string dashboardId, string startTime, string endTime, string region, string subscriptionId, string resourceGroupName, string managedClusterName, string containerAppName, string revisionName);
    }
}
