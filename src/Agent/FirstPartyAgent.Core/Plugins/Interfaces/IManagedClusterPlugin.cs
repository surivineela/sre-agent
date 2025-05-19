// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Interfaces;

public interface IManagedClusterPlugin
{
    Task<string> GetASIPageForManagedCLuster(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId);
}
