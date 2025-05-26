// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
using System.ComponentModel;
using FirstPartyAgent.Plugins;

namespace FirstPartyAgent.Core.Plugins.Interfaces;

public interface IManagedEnvironmentPlugin
{
    Task<string> GetManagedEnvironmentInformation(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId, SamplingOptions? samplingOptions = null);

    Task<string> GetChangesInManagedEnvironment(string region, DateTime fromDate, DateTime toDate, Guid customerSubscriptionId, string managedEnvironmentName);
    Task<string> GetASIPageForManagedEnvironment(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId);

    Task<string> GetManagedClusterEnvironmentResourceId(string region, DateTime fromDate, DateTime toDate, string managedClusterName);

    Task<string> GetManagedEnvironmentProvisioningStatus(string region, DateTime fromDate, DateTime toDate, string environmentName, string resourceGroupName, string subscriptionId);

    Task<string> GetManagedEnvironmentAdminEvents(string region, DateTime fromDate, DateTime toDate, string environmentName, string resourceGroupName, string subscriptionId);
}
