// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.Services.Interfaces;

namespace Agent.Core.Plugins.Definitions
{
    // These are tools exposed to any-sub agent that uses this plugin but mostly it will be used by 'RevisionAgent'
    // Note!!: If this plugin is used by other agent, then we are mixing the concerns and we need to refactor this plugin
    [AgentToolPlugin]
    public class RCAContainerAppRevisionPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;
        private readonly IKustoDashboardPlugin _kustoDashboardPlugin;

        public RCAContainerAppRevisionPluginDefinition(IKustoPluginChat kustoPlugin, IKustoDashboardPlugin kustoDashboardPlugin)
        {
            _kustoPlugin = kustoPlugin;
            _kustoDashboardPlugin = kustoDashboardPlugin;
        }

        [Description(
            """
            Retrieve active revisions with configuration, workload profile, scaling settings, and app status.
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
            - AppReadyForTrafficState: Traffic readiness status.
            """
        )]
        public Task<string> ListRevisionsForRCA([Description("Azure region.")] string region, [Description("Start time of the query.")] DateTime fromDate, [Description("End time of the query.")] DateTime toDate, [Description("Name of the container app.")] string containerAppName, [Description("Name of the resource group.")] string resourceGroupName, [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("ListRevisions", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "containerAppName", containerAppName },
                        { "resourceGroupName", resourceGroupName },
                        { "subscriptionId", subscriptionId }
                });
        }

        [Description(
            """
            Retrieve HttpScaler events and scaling-related activities for a specific container app within a selected time range.

            This function is essential for diagnosing scaling issues, including:
            - HTTP-based autoscaling behavior
            - KEDA scaler failures
            - Scale-in and scale-out events
            - Missed scale-to-zero transitions
            - Anomalous scaling patterns at revision or container app level.

            Projects:
            - PreciseTimeStamp: When the scaling event occurred.
            - EnvironmentName: Name of the cluster hosting the container app.
            - Msg: Detailed message describing the scaling activity or failure reason.
            """
        )]
        public Task<string> GetHttpScalerEventsForContainerApp([Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("provide sampling inputs")] SamplingOptions samplingOptions)
        {
            var parm = new Dictionary<string, string> {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "containerAppName", containerAppName },
                    { "resourceGroupName", resourceGroupName },
                    { "subscriptionId", subscriptionId },
                    { "managedClusterName", managedClusterName }
                };

            return _kustoPlugin.ExecuteLocalFunctionAsync("GetHttpScalerEventsForContainerApp", region, parm, samplingOptions: samplingOptions);
        }

        [Description(
            """
            Retrieve KEDA Operator events related to scaling actions or failures for a container app.
            Projects:
            - LogTime: Log timestamp.
            - Level: Event severity(Info / Error).
            - Msg: Operator event message.
            - KedaVersion: Current KEDA version
            """
        )]
        public Task<string> GetKedaOperatorEventsForContainerApp([Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster name.")] string managedClusterName,
            [Description("Name of the containerapp name.")] string containerAppName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetKedaOperatorEventsForContainerApp", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "managedClusterName", managedClusterName },
                        { "containerAppName", containerAppName },
                });
        }

        [Description(
            """
            Retrieve ASI page url for revision
            """
        )]
        public async Task<string> GetASIPageForRevision([Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the container app revison.")] string revisionName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            var clusterName = await _kustoPlugin.ExecuteFunctionAsync("GetManagedClusterName", region,
                new Dictionary<string, string> {
                        { "containerAppNameParam", containerAppName },
                        { "resourceGroupParam", resourceGroupName },
                        { "subscriptionParam", subscriptionId }
                });

            var basePath = "/services/ACA Azure Container Apps/pages/Container App Revision";
            var cleanPath = Uri.EscapeDataString(basePath); // encodes spaces etc.

            var query = $"EnvironmentName={Uri.EscapeDataString(clusterName.Result.Trim())}" +
                        $"&Name={Uri.EscapeDataString(revisionName)}" +
                        $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("M/d/yyyy hh:mm:ss tt"))}" +
                        $"&globalTo={Uri.EscapeDataString(toDate.ToString("M/d/yyyy hh:mm:ss tt"))}";

            var adxUri = $"https://asi.azure.ms{cleanPath}?{query}";

            return $"ASI Page for revsions {adxUri}";
        }

        [Description(
            """
            Retrieve replica counts and HTTP request counts for a revision (or all revisions) over time to diagnose scaling issues.
            Detect potential problems where replicas exist but no traffic is received.
            Projects:
            - Timestamp: Timestamp for the data point.
            - Revision: Name of the revision.
            - ReplicaCount: Number of active replicas at the timestamp.
            - Status: HTTP response status (e.g., 200, 503).
            - Requests: Number of HTTP requests for that status.

            ⚠️ Important Diagnostic Logic:
            - If ReplicaCount > 0 and Requests == 0, it may indicate a scaling issue, a stuck scale-out, or a service issue requiring deeper investigation.
            """
        )]
        public Task<string> GetRevisionTrafficWithReplicaCount([Description("Azure region.")] string region, [Description("Start time.")] DateTime fromDate, [Description("End time.")] DateTime toDate, [Description("Revision name.")] string revisionName, [Description("App name.")] string containerAppName, [Description("Resource group.")] string resourceGroupName, [Description("Subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetRevisionReplicaAndTraffic", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "revisionName", revisionName },
                        { "containerAppName", containerAppName },
                        { "resourceGroupName", resourceGroupName },
                        { "subscriptionId", subscriptionId },
                });
        }

        [Description(
            """
            Return Container Apps Revision Status for a given container app revision in a time range
            """
        )]
        public Task<string> ContainerAppRevisionStatus([Description("Azure region.")] string region, [Description("Start time.")] DateTime fromDate, [Description("End time.")] DateTime toDate, [Description("Revision name.")] string revisionName, [Description("App name.")] string containerAppName, [Description("Resource group.")] string resourceGroupName, [Description("Subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetRevisionStatus", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "revisionName", revisionName },
                        { "containerAppName", containerAppName },
                        { "resourceGroupName", resourceGroupName },
                        { "subscriptionId", subscriptionId },
                });
        }

        [Description("Return Replica Count of revision for a given time range")]
        public async Task<string> GetReplicaCount([Description("Azure region.")] string region, [Description("Start time.")] DateTime fromDate, [Description("End time.")] DateTime toDate, [Description("Revision name.")] string revisionName, [Description("App name.")] string containerAppName, [Description("Resource group.")] string resourceGroupName, [Description("Subscription ID.")] string subscriptionId)
        {
            string query = $@"
    let startTime = datetime(""{fromDate}"");
    let endTime = datetime(""{toDate}"");
    let cappSubscription = ""{subscriptionId}"";
    let cappResourceGroup = ""{resourceGroupName}"";
    let cappName = ""{containerAppName}"";
    let cappRevisionName = ""{revisionName}"";
    let appArmId = strcat(""/subscriptions/"",cappSubscription,""/resourceGroups/"",cappResourceGroup,""/providers/Microsoft.App/containerApps/"",cappName);
    let genevaAccountName = ""ContainerAppsMdm"";
    let dimension_list = ""'containerAppArmId','revisionName'"";
    let theSchema = datatable (TimestampUtc: datetime, revisionName: string, Max: real) [];
    let sampling = ""Max"";
    let duration = endTime - startTime;
    let bins = datatable(span: timespan, bucket: timespan, mdm_bucket: string) [
            5m, 1m, '1m',
            1d, 1m, '1m',
            2d, 15m, '15m',
            3d, 30m, '30m',
            7d, 1h, '1h',
        ];
    let spans = bins | where duration >= span | top 1 by span desc;
    let bucket = coalesce(toscalar(spans | project bucket), 1d);
    let mdm_bucket = coalesce(toscalar(spans | project mdm_bucket), '1d');
    let mdmData = evaluate geneva_metrics_request(
        genevaAccountName, 
        strcat(
            @""metricNamespace('k4apps-metrics')""
            @"".metric('Replicas')""
            @"".dimensions("",dimension_list, "")""
            @"".samplingTypes('"",sampling,""')""
            @""| where containerAppArmId == '"", appArmId,""' ""
            @""| zoom Max = max("",sampling,"") by "", mdm_bucket
        ),
        startTime,
        endTime
    );
    union theSchema, mdmData
    | project Timestamp = TimestampUtc, Revision = revisionName, Max, appArmId
    | where Revision == cappRevisionName
    | order by Timestamp asc, Revision asc;
    ";

            var result = await _kustoPlugin.ExecuteKustoQuery(region, query);

            return result.Result;
        }

        [Description(
            """
            Retrieve active sessions (start/stop/running changes) for a revision.
            Projects: 
            - StartTime: Session start timestamp.
            - EndTime: Session end timestamp.
            - Content: The running state(e.g., Running, Stopped).
            - GroupBy: The revision name.
            - Health: Health status derived from state.
            """
        )]
        public Task<string> GetActiveRevisionSessions([Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app revision.")] string revisionName,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetActiveRevisionSessions", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "revisionName", revisionName },
                        { "containerAppName", containerAppName },
                        { "resourceGroupName", resourceGroupName },
                        { "subscriptionId", subscriptionId }
                });
        }

        [Description(
            """
            Retrieve HPA (Horizontal Pod Autoscaler) current and target metric values over time for a revision.
            Projects:
            - Timestamp: The timestamp of metric capture.
            - Legend: Metric type(e.g., cpu: current, memory: target).
            - Value: The numeric value of the metric.
            """
        )]
        public Task<string> GetHpaHeartbeatMetrics([Description("Azure region.")] string region,
            [Description("Start time for metrics.")] DateTime fromDate,
            [Description("End time for metrics.")] DateTime toDate,
            [Description("Name of the revision.")] string revisionName,
            [Description("Container app name.")] string containerAppName,
            [Description("Resource group name.")] string resourceGroupName,
            [Description("Subscription ID.")] string subscriptionId,
            [Description("Managed cluster name.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetHpaHeartbeatMetrics", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "revisionName", revisionName },
                        { "containerAppName", containerAppName },
                        { "resourceGroupName", resourceGroupName },
                        { "subscriptionId", subscriptionId },
                        { "managedClusterName", managedClusterName }
                });
        }

        [Description(
            """
            Retrieve HPA (Horizontal Pod Autoscaler) current and target metric values over time for a revision.
            Projects:
            - Timestamp: The timestamp of metric capture.
            - Legend: Metric type(e.g., cpu: current, memory: target).
            - Value: The numeric value of the metric.
            """
        )]
        public Task<string> GetRevisionSpecChanges([Description("Azure region.")] string region,
            [Description("Start time.")] DateTime fromDate,
            [Description("End time.")] DateTime toDate,
            [Description("Revision name.")] string revisionName,
            [Description("App name.")] string containerAppName,
            [Description("Resource group.")] string resourceGroupName,
            [Description("Subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetRevisionSpecChanges", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "revisionName", revisionName },
                        { "containerAppName", containerAppName },
                        { "resourceGroupName", resourceGroupName },
                        { "subscriptionId", subscriptionId }
                });
        }

        [Description(
            """
            Retrieves all ARM(Azure resource manager) operations for the container app. These include PUT,UPDATE,DELETE and the appropriate status codes pertaining to those operations.
            Projects:
            - Timestamp: The timestamp of metric capture.
            - Legend: Metric type(e.g., cpu: current, memory: target).
            - Value: The numeric value of the metric.
            """
        )]
        public Task<string> GetArmOperations([Description("Azure region.")] string region,
            [Description("Start time.")] DateTime fromDate,
            [Description("End time.")] DateTime toDate,
            [Description("App name.")] string containerAppName,
            [Description("Resource group.")] string resourceGroupName,
            [Description("Subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetArmCalls", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "containerAppName", containerAppName },
                        { "resourceGroupName", resourceGroupName },
                        { "subscriptionId", subscriptionId }
                });
        }

        [Description(
            """
            Retrieve EventProcessor events for a revision where no replica is associated.
            Projects:
            - PreciseTimeStamp: Timestamp of event.
            - RevisionName: Revision associated.
            - Reason: Why the event occurred.
            - Msg: Additional event message details.
            """
        )]
        public Task<string> GetEventProcessorEventsWithoutReplica([Description("Azure region.")] string region,
            [Description("Start time.")] DateTime fromDate,
            [Description("End time.")] DateTime toDate,
            [Description("Revision name.")] string revisionName,
            [Description("App name.")] string containerAppName,
            [Description("Resource group.")] string resourceGroupName,
            [Description("Subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEventProcessorEventsWithoutReplica", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "revisionName", revisionName },
                        { "containerAppName", containerAppName },
                        { "resourceGroupName", resourceGroupName },
                        { "subscriptionId", subscriptionId }
                });
        }

        [Description(
            """
            Retrieve the latest pod heartbeat status for a revision.
            Projects:
            - PodName: The pod's name.
            - EnvironmentName: The cluster where the pod runs.
            - Status: Current pod status or 'Shut Down'.
            - PreciseTimeStamp: Last heartbeat timestamp.
            - LegionPodName: If it's a 'consumption' workload pod.
            """
        )]
        public Task<string> GetPodHeartbeatStatus([Description("Azure region.")] string region,
            [Description("Start of the time range.")] DateTime fromDate,
            [Description("End of the time range.")] DateTime toDate,
            [Description("Revision name.")] string revisionName,
            [Description("App name.")] string containerAppName,
            [Description("Resource group name.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHeartbeatStatus", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "revisionName", revisionName },
                        { "containerAppName", containerAppName },
                        { "resourceGroupName", resourceGroupName },
                        { "subscriptionId", subscriptionId }
                });
        }

        [Description(
            """
            Retrieve internal EventProcessor events for a specific pod inside a revision.
            Projects:
            - PreciseTimeStamp: Event timestamp.
            - Type: Type of event (Normal/Error).
            - Msg: Event message.
            - Reason: Short reason description.
            - Count: How many times occurred.
            - EventSource: Event origin.
            - ReplicaName: The pod's replica name.
            - RevisionName: Associated revision.
            - Level: Mapped to Info or Error.
            """
        )]
        public Task<string> GetInternalEventProcessorEventsForPod([Description("Azure region.")] string region,
            [Description("Start timestamp.")] DateTime fromDate,
            [Description("End timestamp.")] DateTime toDate,
            [Description("Name of the revision.")] string revisionName,
            [Description("Pod name within the revision.")] string podName,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetInternalEventProcessorEventsForPod", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "revisionName", revisionName },
                        { "podName", podName },
                        { "containerAppName", containerAppName },
                        { "resourceGroupName", resourceGroupName },
                        { "subscriptionId", subscriptionId }
                });
        }

        [Description("Get Legion errors for a given revision")]
        public Task<string> GetLegionErrors(
            [Description("Azure region.")] string region,
            [Description("Start time.")] DateTime fromDate,
            [Description("End time.")] DateTime toDate,
            [Description("Revision name.")] string revisionName)
        {
            if (toDate - fromDate > TimeSpan.FromDays(1))
            {
                throw new ArgumentException("Legion queries are expensive and should be limited to a 1 day.");
            }

            return _kustoPlugin.ExecuteLocalFunctionOnClusterAsync("GetLegionErrors",
                "legioneus.eastus", "legion",
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "revisionName", revisionName },
                        { "region", region }
                });
        }

        [Description(
            """
            Retrieve readiness/liveness/startup probe failures for a specific Azure container app revision.
            Projects:
            - msg: Log message of the probe failure.
            - count: Number of times the probe failed with the same message consecutively.
            - replicaName: Name of the replica where the failure occurred.
            - revisionName: Name of the container app revision.
            - level: Severity level of the failure (e.g., error, warning).
            """
        )]
        public Task<string> GetHealthProbeFailures(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the revision.")] string revisionName,
            [Description("provide sampling inputs")] SamplingOptions sampling)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetHealthProbeFailures", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "revisionName", revisionName },
                        { "containerAppName", containerAppName }
                });
        }

        [Description(
            """
            Retrieve the latest health probe settings for the Azure container app within the specified period.
        
            Projects:
            - containers: List of containers in the container app with the each probe setting if set by the customer.
            """
        )]
        public Task<string> GetHealthProbeSettings(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Subscription ID.")] string subscriptionId,
            [Description("Resource group name.")] string resourceGroupName,
            [Description("Name of the container app.")] string containerAppName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetHealthProbeSettings", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "subscriptionId", subscriptionId },
                        { "resourceGroupName", resourceGroupName },
                        { "containerAppName", containerAppName }
                });
        }

        [Description("Generate a dashboard link for customer issues related to container app revisions.")]
        public string GenerateRevisionCustomerIssuesDashboardLink(
            [Description("Start time for the dashboard.")] string startTime,
            [Description("End time for the dashboard.")] string endTime,
            [Description("Azure region.")] string region,
            [Description("Azure subscription ID.")] string subscriptionId,
            [Description("Resource group name.")] string resourceGroupName,
            [Description("Managed cluster name.")] string managedClusterName,
            [Description("Container app name.")] string containerAppName,
            [Description("Revision name.")] string revisionName)
        {
            return _kustoDashboardPlugin.GenerateDashboardLink("5563467d-adf2-4a55-b390-8d71e672e13b", startTime, endTime, region, subscriptionId, resourceGroupName, managedClusterName, containerAppName, revisionName);
        }
    }
}
