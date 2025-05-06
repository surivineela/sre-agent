// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------
namespace FirstPartyAgent.Core.Plugins.Interfaces;

// [MENDATORY]
public interface IContainerAppRevisionPlugin
{
    Task<string> ListRevisions(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId);

    Task<string> GetRevisionTrafficWithReplicaCount(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId);

    Task<string> GetReplicaCount(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId);

    Task<string> ContainerAppRevisionStatus(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId);

    Task<string> GetActiveRevisionSessions(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId);

    Task<string> GetHpaHeartbeatMetrics(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId);

    Task<string> GetRevisionSpecChanges(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId);

    Task<string> GetEventProcessorEventsWithoutReplica(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId);

    Task<string> GetPodHeartbeatStatus(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId);

    Task<string> GetInternalEventProcessorEventsForPod(string region, DateTime fromDate, DateTime toDate, string revisionName, string podName, string containerAppName, string resourceGroupName, string subscriptionId);

    Task<string> GetHttpScalerEventsForContainerApp(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId);

    Task<string> GetKedaOperatorEventsForContainerApp(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId);
    Task<string> GetASIPageForRevision(string region, DateTime fromDate, DateTime toDate, string containerAppName, string revisionName, string resourceGroupName, string subscriptionId);





}
