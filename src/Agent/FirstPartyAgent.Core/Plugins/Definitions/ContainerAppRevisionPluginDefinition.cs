// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.Tracing;
using System.Reactive;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using Agent.Data.DatabaseClients.GraphDbClient;
using Azure.ResourceManager.AppContainers.Models;
using Castle.Components.DictionaryAdapter;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Google.Protobuf.WellKnownTypes;
using Microsoft.AspNetCore.Routing;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.Kiota.Abstractions;
using Microsoft.SemanticKernel;
using ScottPlot.Colormaps;
using ScottPlot.Hatches;
using ScottPlot.Palettes;
using static System.Net.WebRequestMethods;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    // [MENDATORY]
    // These are tools exposed to any-sub agent that uses this plugin but mostly it will be used by 'RevisionAgent'
    // Note!!: If this plugin is used by other agent, then we are mixing the concerns and we need to refactor this plugin
    public class ContainerAppRevisionPluginDefinition
    {
        private readonly IContainerAppRevisionPlugin _plugin;

        public ContainerAppRevisionPluginDefinition(IContainerAppRevisionPlugin Plugin)
        {
            _plugin = Plugin;
        }

        //[KernelFunction(KernelFunctionNames.ACA.CallKustoQuery)]
        //public async Task<string> CallKustoQuery(KernelArguments args)
        //{
        //    var name = args["functionName"].ToString();
        //    var region = args["region"].ToString();
        //    var kqlArgs = args.Where(k => k.Key != "functionName")
        //                      .ToDictionary(k => k.Key, k => k.Value?.ToString());

        //    return await _plugin.CallKustoQuery(region,name, kqlArgs);
        //}

        [KernelFunction(KernelFunctionNames.ACA.ListRevisions)]
        [Description(
    @"Retrieve active revisions with configuration, workload profile, scaling settings, and app status.
    Projects:
    - Name: Revision name.
    - EnvironmentName: Cluster name.
    - ContainerAppName: App name.
    - Namespace: K8s namespace.
    - ReadyReplicas: Current ready replicas.
    - WorkloadProfileNameUpdated: Workload profile name.
    - AppType: App type(e.g., HTTP/GRPC).
    - HttpOptionsEnabled: HTTP enabled or not.
    - MinReplicaCount/MaxReplicaCount: Scaling settings.
    - RevisionProvisioningState: Provisioning status.
    - RevisionRunningState: Running status.
    - AppReadyForTrafficState: Traffic readiness status."
)]public Task<string> ListRevisions(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
        {
            return _plugin.ListRevisions(region.NormalizeLocation(), fromDate, toDate, containerAppName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetHttpScalerEventsForContainerApp)]
        [Description(
    @"Retrieve HttpScaler events and scaling-related activities for a specific container app within a selected time range.
    This function is essential for diagnosing scaling issues, including:
    - HTTP-based autoscaling behavior
    - KEDA scaler failures
    - Scale-in and scale-out events
    - Missed scale-to-zero transitions
    - Anomalous scaling patterns at revision or container app level.
    
    Projects:
    - PreciseTimeStamp: When the scaling event occurred.
    - EnvironmentName: Name of the cluster hosting the container app.
    - Msg: Detailed message describing the scaling activity or failure reason."
)]
        public Task<string> GetHttpScalerEventsForContainerApp(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
        {
            return _plugin.GetHttpScalerEventsForContainerApp(region.NormalizeLocation(), fromDate, toDate, containerAppName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetKedaOperatorEventsForContainerApp)]
        [Description(
    @"Retrieve KEDA Operator events related to scaling actions or failures for a container app.
    Projects:
    - LogTime: Log timestamp.
    - Level: Event severity(Info / Error).
    - Msg: Operator event message."
)] public Task<string> GetKedaOperatorEventsForContainerApp(string region, DateTime fromDate, DateTime toDate, string containerAppName, string resourceGroupName, string subscriptionId)
        {
            return _plugin.GetKedaOperatorEventsForContainerApp(region.NormalizeLocation(), fromDate, toDate, containerAppName, resourceGroupName, subscriptionId);
        }


        [KernelFunction(KernelFunctionNames.ACA.GetRevisionTrafficWithReplicaCount)]
        [Description(
    @"Retrieve replica counts and HTTP request counts for a revision (or all revisions) over time to diagnose scaling issues.
    Detect potential problems where replicas exist but no traffic is received.
    Projects:
    - Timestamp: Timestamp for the data point.
    - Revision: Name of the revision.
    - ReplicaCount: Number of active replicas at the timestamp.
    - Status: HTTP response status (e.g., 200, 503).
    - Requests: Number of HTTP requests for that status.
    
    ⚠️ Important Diagnostic Logic:
    - If ReplicaCount > 0 and Requests == 0, it may indicate a scaling issue, a stuck scale-out, or a service issue requiring deeper investigation."
)]
        public Task<string> GetRevisionTrafficWithReplicaCount(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
        {
            return _plugin.GetRevisionTrafficWithReplicaCount(region.NormalizeLocation(), fromDate, toDate, revisionName, containerAppName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.ContainerAppRevisionStatus)]
        [Description("Return Container Apps Revision Statu for a given container app revision in a time range")]
        public Task<string> ContainerAppRevisionStatus(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
        {
            return _plugin.ContainerAppRevisionStatus(region.NormalizeLocation(), fromDate, toDate, revisionName, containerAppName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetReplicaCount)]
        [Description("Return Replica Count of revision for a given time range")]
        public Task<string> GetReplicaCount(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
        {
            return _plugin.GetReplicaCount(region.NormalizeLocation(), fromDate, toDate, revisionName, containerAppName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetActiveRevisionSessions)]
        [Description(
         @"Retrieve active sessions (start/stop/running changes) for a revision.
     Projects: 
    - StartTime: Session start timestamp.
    - EndTime: Session end timestamp.
    - Content: The running state(e.g., Running, Stopped).
    - GroupBy: The revision name.
    - Health: Health status derived from state."
)]
        public Task<string> GetActiveRevisionSessions(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
        {
            return _plugin.GetActiveRevisionSessions(region.NormalizeLocation(), fromDate, toDate, revisionName, containerAppName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetHpaHeartbeatMetrics)]
        [Description(
    @"Retrieve HPA (Horizontal Pod Autoscaler) current and target metric values over time for a revision.
    Projects:
    - Timestamp: The timestamp of metric capture.
    - Legend: Metric type(e.g., cpu: current, memory: target).
    - Value: The numeric value of the metric."
)]
        public Task<string> GetHpaHeartbeatMetrics(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
        {
            return _plugin.GetHpaHeartbeatMetrics(region.NormalizeLocation(), fromDate, toDate, revisionName, containerAppName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetRevisionSpecChanges)]
        [Description(
    @"Retrieve HPA (Horizontal Pod Autoscaler) current and target metric values over time for a revision.
    Projects:
    - Timestamp: The timestamp of metric capture.
    - Legend: Metric type(e.g., cpu: current, memory: target).
    - Value: The numeric value of the metric."
)]
        public Task<string> GetRevisionSpecChanges(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
        {
            return _plugin.GetRevisionSpecChanges(region.NormalizeLocation(), fromDate, toDate, revisionName, containerAppName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetEventProcessorEventsWithoutReplica)]
        [Description(
    @"Retrieve EventProcessor events for a revision where no replica is associated.
    Projects:
    - PreciseTimeStamp: Timestamp of event.
    - RevisionName: Revision associated.
    - Reason: Why the event occurred.
    - Msg: Additional event message details."
)]public Task<string> GetEventProcessorEventsWithoutReplica(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
        {
            return _plugin.GetEventProcessorEventsWithoutReplica(region.NormalizeLocation(), fromDate, toDate, revisionName, containerAppName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetPodHeartbeatStatus)]
        [Description(
    @"Retrieve the latest pod heartbeat status for a revision.
    Projects:
    - PodName: The pod's name.
    - EnvironmentName: The cluster where the pod runs.
    - Status: Current pod status or 'Shut Down'.
    - PreciseTimeStamp: Last heartbeat timestamp.
    - LegionPodName: If it's a 'consumption' workload pod."
)]
        public Task<string> GetPodHeartbeatStatus(string region, DateTime fromDate, DateTime toDate, string revisionName, string containerAppName, string resourceGroupName, string subscriptionId)
        {
            return _plugin.GetPodHeartbeatStatus(region.NormalizeLocation(), fromDate, toDate, revisionName, containerAppName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetInternalEventProcessorEventsForPod)]
        [Description(
    @"Retrieve internal EventProcessor events for a specific pod inside a revision.
    Projects:
    - PreciseTimeStamp: Event timestamp.
    - Type: Type of event (Normal/Error).
    - Msg: Event message.
    - Reason: Short reason description.
    - Count: How many times occurred.
    - EventSource: Event origin.
    - ReplicaName: The pod's replica name.
    - RevisionName: Associated revision.
    - Level: Mapped to Info or Error."
)]public Task<string> GetInternalEventProcessorEventsForPod(string region, DateTime fromDate, DateTime toDate, string revisionName, string podName, string containerAppName, string resourceGroupName, string subscriptionId)
        {
            return _plugin.GetInternalEventProcessorEventsForPod(region.NormalizeLocation(), fromDate, toDate, revisionName, podName, containerAppName, resourceGroupName, subscriptionId);
        }



    }
}
