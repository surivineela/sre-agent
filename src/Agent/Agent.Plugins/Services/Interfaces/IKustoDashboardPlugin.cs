// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Plugins.Kusto;

namespace Agent.Plugins.Services.Interfaces
{
    /// <summary>
    /// Generate Azure Data Explorer Dashboard Links for Container Apps Investigations
    /// </summary>
    public interface IKustoDashboardPlugin
    {
        string GenerateDashboardLink(string dashboardId, string startTime, string endTime, AzureRegion region, string subscriptionId, string resourceGroupName, string managedClusterName, string containerAppName, string revisionName);
    }
}
