// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.ComponentModel;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Interfaces;

// [MENDATORY]
public interface IManagedEnvironmentPlugin
{
    Task<string> GetManagedEnvironmentInformation(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId, SamplingOptions? samplingOptions = null);

    Task<string> GetASIPageForManagedEnvironment(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId);
}
