// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace FirstPartyAgent.Core.Plugins.Interfaces;

public interface IContainerAppSessionsPlugin
{
    Task<string> GetSessionPoolInfo(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string sessionPoolName);

    Task<string> GetChangesInSessionPool(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string sessionPoolName);

    Task<string> GetSessionPodLogs(string region, DateTime fromDate, DateTime toDate, string podName);

    Task<string> GetSessionPoolCreateOrUpdateLogs(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string sessionPoolName);

    Task<string> GetCodeInterpreterSessionExecutionEventLogs(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string sessionPoolName, string sessionIdentifier = "");

    Task<string> GetCustomContainerSessionActivatorLogs(string region, DateTime fromDate, DateTime toDate, string subscriptionId, string resourceGroupName, string sessionPoolName, string managedEnvironmentName);

}
