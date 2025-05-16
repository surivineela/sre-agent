// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Interfaces;

public interface IHealthProbePlugin
{
    Task<string> GetHealthProbeFailures(string region, DateTime fromDate, DateTime toDate, string containerAppName, string revisionName);

    Task<string> GetHealthProbeSettings(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string containerAppName);
}
