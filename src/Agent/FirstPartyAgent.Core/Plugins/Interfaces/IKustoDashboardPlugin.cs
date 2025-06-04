// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.Extensions.AI;

namespace FirstPartyAgent.Plugins
{
    /// <summary>
    /// Generate Azure Data Explorer Dashboard Links for Container Apps Investigations
    /// </summary>
    public interface IKustoDashboardPlugin
    {
        public string GenerateDashboardLink(string dashboardId, string startTime, string endTime, string region, string subscriptionId, string resourceGroupName, string containerAppName, string revisionName);

    }
}
