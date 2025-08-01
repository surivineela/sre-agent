// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Plugins;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.Services.Interfaces;

namespace Agent.Core.Plugins.Definitions
{
    // These are tools exposed to any-sub agent that uses this plugin but mostly it will be used by 'RevisionAgent'
    // Note!!: If this plugin is used by other agent, then we are mixing the concerns and we need to refactor this plugin
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppRevisionPluginDefinition
    {
        private readonly IKustoPlugin _kustoPlugin;
        private readonly IKustoDashboardPlugin _kustoDashboardPlugin;

        public RCAContainerAppRevisionPluginDefinition(IKustoPlugin kustoPlugin, IKustoDashboardPlugin kustoDashboardPlugin)
        {
            _kustoPlugin = kustoPlugin;
            _kustoDashboardPlugin = kustoDashboardPlugin;
        }

                [Description("""
Purpose:
Retrieves all revisions with configuration, workload profile, scaling settings, and app status.

Scenario:
Use this tool to list all revisions and their configuration details for a container app.

Output:
Returns tab-separated table data in CSV format. Column headers:
- Name
- ManagedClusterName
- ContainerAppName
- Namespace
- ReadyReplicas
- CreationTimestamp
- WorkloadProfileNameUpdated
- RestartTime
- AppType
- HttpOptionsEnabled
- HttpOptionsExternal
- HttpOptionsPort
- Http2Enabled
- HttpsOnly
- Stopped
- MinReplicaCount
- MaxReplicaCount
- HttpScalingRuleName
- HttpScalingRuleConcurrentRequests: Concurrent requests limit
- ObservedGeneration: Observed generation
- RevisionProvisioningState
- RevisionHealthStatus
- RevisionRunningState
- AppReadyForTrafficState: Traffic readiness state
- PreciseTimeStamp
- legionRevisionName
"""
)]
        public Task<string> ListRevisionsForRCA(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
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

                [Description("""
Purpose:
Retrieves events that is about HTTP scaler for a container app.

Scenario:
Use this tool to diagnose scaling issues when a container app has HTTP scaler.

Output:
Returns tab-separated table data in CSV format. Column headers:
- PreciseTimeStamp: When the scaling event occurred
- EnvironmentName: Name of the cluster hosting the container app
- msg: Detailed message describing the scaling activity or failure reason
"""
)]
        public Task<string> GetHttpScalerEventsForContainerApp(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Sampling options for the query.")] SamplingOptions samplingOptions)
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

        [Description("""
        Purpose:
        Retrieves the precise maximum and minimum values of metrics (s1_upstream_rq_total, s1_upstream_cx_active, s1_upstream_rq_active, s1_upstream_cx_total) for the HTTP scaler in real time, without any data collection interval.


        Scenario:
        Use this tool to verify whether the metrics value of HTTP scaler matches the scaling behavior of HPA. 

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - MetricName: Four types of HTTP metrics name will be returned: upstream_rq_total/upstream_cx_active/upstream_rq_active/upstream_cx_total
        - MaxValue
        - MinValue
        - DesiredReplica_Max
        - DesiredReplica_Min
        """
        )]
        public Task<string> GetHttpScalerMetricsForContainerApp(
    [Description("Azure region.")] string region,
    [Description("Start time of the query.")] DateTime fromDate,
    [Description("End time of the query.")] DateTime toDate,
    [Description("Name of the container app.")] string containerAppName,
    [Description("Name of the managed cluster.")] string managedClusterName,
    [Description("concurrentRequests setting from http scaling rule.")] string concurrentRequests,
    [Description("minReplica setting from http scaling rule.")] string minReplica,
    [Description("maxReplica setting from http scaling rule.")] string maxReplica,
    [Description("Sampling options for the query.")] SamplingOptions samplingOptions)
        {
            var parm = new Dictionary<string, string> {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "containerAppName", containerAppName },
                    { "managedClusterName", managedClusterName },
                    { "concurrentRequests", concurrentRequests },
                    { "minReplica", minReplica },
                    { "maxReplica", maxReplica }
                };

            return _kustoPlugin.ExecuteLocalFunctionAsync("GetHttpScalerMetricsForContainerApp", region, parm, samplingOptions: samplingOptions);
        }

        [Description("""
Purpose:
Retrieves KEDA Operator events related to scaling actions or failures for a container app.

Scenario:
Use this tool to review KEDA Operator events and diagnose scaling issues.

Output:
Returns tab-separated table data in CSV format. Column headers:
- PreciseTimeStamp: Log timestamp
- Log: Operator event message
- KedaVersion: Current KEDA version
"""
)]
        public Task<string> GetKedaOperatorEventsForContainerApp(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the container app.")] string containerAppName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetKedaOperatorEventsForContainerApp", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "managedClusterName", managedClusterName },
                        { "containerAppName", containerAppName },
                });
        }

        [Description("""
        Purpose:
        Retrieves a direct App Service Insights (ASI) page URL for a specific revision.

        Scenario:
        Use this tool to get a diagnostic insights link for a container app revision over a specified time range.

        Output:
        Returns a string containing the ASI page URL:
        - ASIPageUrl: Direct URL to the ASI diagnostics page for the specified revision
        """
        )]
        public async Task<string> GetASIPageForRevision(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the container app revision.")] string revisionName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            var clusterName = await _kustoPlugin.ExecuteFunctionInternalAsync("GetManagedClusterName", region,
                new Dictionary<string, string> {
                        { "containerAppNameParam", containerAppName },
                        { "resourceGroupParam", resourceGroupName },
                        { "subscriptionParam", subscriptionId }
                });

            var basePath = "/services/ACA Azure Container Apps/pages/Container App Revision";
            var cleanPath = Uri.EscapeDataString(basePath);

            var query = $"EnvironmentName={Uri.EscapeDataString(clusterName.Result.Trim())}" +
                        $"&Name={Uri.EscapeDataString(revisionName)}" +
                        $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("M/d/yyyy hh:mm:ss tt"))}" +
                        $"&globalTo={Uri.EscapeDataString(toDate.ToString("M/d/yyyy hh:mm:ss tt"))}";

            var adxUri = $"https://asi.azure.ms{cleanPath}?{query}";

            return $"ASI Page for revsions {adxUri}";
        }

        [Description("""
        Purpose:
        Retrieves replica counts and HTTP request counts for a revision over time.

        Scenario:
        Use this tool to diagnose scaling issues and detect periods where replicas exist but no traffic is received.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - Timestamp: Timestamp for the data point
        - ReplicaCount: Number of active replicas at the timestamp
        - Status: HTTP response status (e.g., 200, 503)
        - Requests: Number of HTTP requests for that status
        """
        )]
        public Task<string> GetRevisionTrafficWithReplicaCount(
            [Description("Azure region.")] string region,
            [Description("Start time.")] DateTime fromDate,
            [Description("End time.")] DateTime toDate,
            [Description("Revision name.")] string revisionName,
            [Description("App name.")] string containerAppName,
            [Description("Resource group.")] string resourceGroupName,
            [Description("Subscription ID.")] string subscriptionId)
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

                [Description("""
Purpose:
Retrieves the status of a container app revision for a given time range.

Scenario:
Use this tool to get the provisioning and running status of a revision.

Output:
Returns tab-separated table data in CSV format with status and state columns as defined in the revision status query.
"""
)]
        public Task<string> ContainerAppRevisionStatus(
            [Description("Azure region.")] string region,
            [Description("Start time.")] DateTime fromDate,
            [Description("End time.")] DateTime toDate,
            [Description("Revision name.")] string revisionName,
            [Description("App name.")] string containerAppName,
            [Description("Resource group.")] string resourceGroupName,
            [Description("Subscription ID.")] string subscriptionId)
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

                [Description("""
Purpose:
Retrieves the replica count of a revision for a given time range.

Scenario:
Use this tool to get the number of replicas for a revision over time.

Output:
Returns tab-separated table data in CSV format. Column headers:
- Timestamp: Timestamp of the data point
- Revision: Name of the revision
- Max: Maximum replica count at the timestamp
- appArmId: ARM ID of the app
"""
)]
        public async Task<string> GetReplicaCount(
            [Description("Azure region.")] string region,
            [Description("Start time.")] DateTime fromDate,
            [Description("End time.")] DateTime toDate,
            [Description("Revision name.")] string revisionName,
            [Description("App name.")] string containerAppName,
            [Description("Resource group.")] string resourceGroupName,
            [Description("Subscription ID.")] string subscriptionId)
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

            var result = await _kustoPlugin.ExecuteKustoQueryInternal(region, query);

            return result.Result;
        }

        [Description(@"""
Purpose:
Retrieves active revisions for the given container app.

Scenario:
Use this tool to get all active revisions for a container app within a specified time range.

Output: Returns tab-separated table data in CSV format. Column headers:
- StartTime: start timestamp 
- EndTime: end timestamp.
- RevisionName: The active revision name.
- State: The running state (e.g., Running, Stopped).
- HealthStatus: Health status derived from state.
"""
)]
        public Task<string> GetActiveRevisions(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetActiveRevisions", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "containerAppName", containerAppName },
                        { "resourceGroupName", resourceGroupName },
                        { "subscriptionId", subscriptionId }
                });
        }

        [Description(@"""
Purpose:
Retrieves the maximum, minimum, and target metric values of different scalers in HPA (Horizontal Pod Autoscaler) without considering tolerance, and the scaling result when tolerance is considered. Metric collection occurs at 30-second intervals, which may result in missed peak values.

Scenario:
Use this tool to check whether HPA triggers the scale-up or scale-down when analyzing scaling issue.

Output:
Returns tab-separated table data in CSV format. Column headers:
- MetricName: Name of the metric.
- MaxValue: Maximum value the metric without considering tolerance.
- MinValue: Minimum value the metric without considering tolerance.
- TargetValue: the target value to trigger scaling without considering tolerance.
- Tolerance: The value of tolerance to work with targetvalue to providing threshold range.
- ScalingResult: The scaling result may be triggered by HPA.
"""
)]
        public Task<string> GetHpaHeartbeatMetrics(
            [Description("Azure region.")] string region,
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

        [Description(@"""
        Purpose:
        Retrieves revision spec changes for a given revision and time range.

        Scenario:
        Use this tool to get changes in the revision specification over time.

        Output: Returns tab-separated table data in CSV format. Column headers:
        - TIMESTAMP: Time of the spec change.
        - PreviousSpec: Previous revision spec.
        - spec: Current revision spec.
        """
        )]
        public Task<string> GetRevisionSpecChanges(
            [Description("Azure region.")] string region,
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

        [Description(@"""
        Purpose:
        Retrieves all ARM (Azure Resource Manager) operations for the container app.

        Scenario:
        Use this tool to get ARM operation events and their health status for a container app.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - StartTime: Timestamp of the operation.
        - Content: Operation method and status code.
        - GroupBy: Managed cluster name.
        - requestBody: Request body content.
        - durationInMilliseconds: Duration of the operation.
        - env_dt_traceId: Trace ID.
        - env_dt_spanId: Span ID.
        - correlationId: Correlation ID.
        - Health: Health status of the operation.
        """
        )]
        public Task<string> GetArmOperations(
            [Description("Azure region.")] string region,
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

        [Description("""
        Purpose:
        Retrieves EventProcessor events for a specific revision or pod.

        Scenario:
        Use this tool when you need to investigate:
        - Revision startup failures or unexpected restarts
        - Revision scale unexpectedly or fail to scale
        - Pod lifecycle events such as container creation, restarts, exits, and node activity.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - StartTime: Timestamp of the first time the event occurred.
        - EndTime: Timestamp of the last time the event occurred.
        - RevisionName: Name of the revision.
        - ReplicaName: Name of the replica.
        - Reason: Reason for the event.
        - Message: Additional event message details.
        """
        )]
        public Task<string> GetEventProcessorEventsForRevision(
            [Description("Azure region.")] string region,
            [Description("Start time.")] DateTime fromDate,
            [Description("End time.")] DateTime toDate,
            [Description("Revision name.")] string revisionName,
            [Description("Pod name within the revision.")] string podName,
            [Description("App name.")] string containerAppName,
            [Description("Resource group.")] string resourceGroupName,
            [Description("Subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEventProcessorEventsForRevision", region,
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

        [Description(@"""
        Purpose:
        Retrieves the latest pod heartbeat status for a revision.

        Scenario:
        Use this tool to check the pod status for a revision.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - PodName: Name of the pod.
        - ManagedClusterName: Name of the cluster.
        - Status: Current pod status or 'Shut Down'.
        - PreciseTimeStamp: Last heartbeat timestamp.
        - legionPodName: Legion pod name if applicable.
        """
        )]
        public Task<string> GetPodHeartbeatStatus(
            [Description("Azure region.")] string region,
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

        [Description(@"""
        Purpose:
        Retrieves Legion errors for a given revision.

        Scenario:
        Use this tool to check Legion error events for a revision.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - TIMESTAMP: Timestamp of the error event.
        - PodName: Name of the pod.
        - severityText: Severity of the error.
        """
        )]
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

        [Description("""
        Purpose:
        Retrieves Legion VK (vertual kubelet) events for a specific pod within a revision.

        Scenario:
        Use this tool when investigating issues for container apps running on Legion. You can also use it to retrieve Legion Virtual Kubelet (VK) events, which can be correlated with revision failures such as provisioning errors, crashes, scheduling problems, and connectivity disruptions.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - TIMESTAMP: The time when the event occurred
        - PodName: The name of the pod associated with the event
        - Message: The message associated with the event, which may include details about the event
        - Error: The error message if the event is an error
        - Method: The name of the method that generated the event (e.g., "legion.DeletePod", "UpdateNodeStatus")
        - Reason: The reason for the event (e.g., "Evicted By Legion", "NC Polling Pending on legion", "Pending pod creation on legion")
        """)]
        public async Task<string> GetLegionVKEventsForContainerAppRevision(
            [Description("The start date for the query")] string fromDate,
            [Description("The end date for the query")] string toDate,
            [Description("The region of the managed cluster")] string region,
            [Description("The name of the managed cluster")] string managedClusterName,
            [Description("Revision name.")] string revisionName)
         {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetLegionVKEventsForContainerAppRevision", region,
                new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "revisionName", revisionName },
                { "managedClusterName", managedClusterName }
             });
        }

        [Description("""
        Purpose:
        Retrieve pod evictions due to Legion Host shutdowns for a specific pod within a revision.

        Scenario:
        Use this tool when the container app is running on Legion and experiencing availability issues. This helps determine if pod deletions or crashes were caused by Legion Host shutdowns.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - PreciseTimeStamp: The time when Legion Host is shutdown
        - PodName: The name of the pod that was shutdown
        - OperationName: The name of the operation that was performed. for example, "MarkPodEvicted"
        - Message: The message associated with the shutdown operation, which may include details about the shutdown operation.
        """)]
        public async Task<string> GetPodEvictionsDueToLegionHostShutdown(
            [Description("The start date for the query")] string fromDate,
            [Description("The end date for the query")] string toDate,
            [Description("The region of the managed cluster")] string region,
            [Description("The name of the managed cluster")] string managedClusterName,
            [Description("Revision name.")] string revisionName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetPodEvictionsDueToLegionHostShutdown", region, new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "revisionName", revisionName },
                { "managedClusterName", managedClusterName },
             }, groupName: "Legion");
        }

        [Description("""
        Purpose:
        Retrieves the status of Legion hosts for a specific pod within a revision, indicating whether the host is enabled, disabled, or deleted.

        Scenario:
        Use this tool when a container app is running on Legion and experiencing availability issues. It helps determine if the Legion host has been deleted or recycled.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - StartTime: Start time of the time window
        - EndTime: End time of the time window
        - CenturionRoleId: Centurion role ID of the Legion host
        - LegionHostStatus: Legion host status during the time window (e.g., Enabled, Disabled, Deleted)
        """)]
        public async Task<string> GetLegionHostStatus(
            [Description("The start date for the query")] string fromDate,
            [Description("The end date for the query")] string toDate,
            [Description("The region of the managed cluster")] string region,
            [Description("Revision name.")] string revisionName,
            [Description("The name of the managed cluster")] string managedClusterName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetLegionHostStatus", region, new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "revisionName", revisionName },
                { "managedClusterName", managedClusterName }
             }, groupName: "Legion");
        }


        [Description(
        """
        Purpose:
        Retrieve HTTP request counts by response status codes for a revision over time every 30min which gives traffic pattern to align it any failures.
        
        Scenario:
        Use this tool to analyze traffic patterns and HTTP response status codes for a revision.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - Timestamp: Timestamp of start of 30min timeslot.
        - Status: HTTP response status (e.g., 2xx, 5xx).
        - Requests: Number of HTTP requests for that status.
        """
        )]
        public Task<string> GetRevisionTrafficStatus(
            [Description("Azure region.")] string region,
            [Description("Start time.")] DateTime fromDate,
            [Description("End time.")] DateTime toDate,
            [Description("Revision name.")] string revisionName,
            [Description("App name.")] string containerAppName,
            [Description("Resource group.")] string resourceGroupName,
            [Description("Subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetRevisionTrafficStatus", region,
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
            Purpose:
            Retrieve number of readiness/liveness/startup probe failures happened at every 30mins timeslots for a specific Azure container app revision.

            Scenario:
            Use this tool to check whether the revision has unexpected probe failures.

            Output:
            Returns tab-separated table data in CSV format. Column headers:
            - TimeSlot: 30-minute slots of time.
            - msg: Log message of the probe failure.
            - FailureCount: Number of times the probe failure happened with the same message within the timeslot.
            """
        )]
        public Task<string> GetHealthProbeFailures(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the revision.")] string revisionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetHealthProbeFailures", region,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "revisionName", revisionName },
                        { "containerAppName", containerAppName }
                });
        }

        [Description(@"""
        Purpose:
        Retrieves the latest health probe settings for a container app.


        Scenario:
        Use this tool to get the health probe configuration for a container app.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - containers: List of containers in the container app with each probe setting if set by the customer.
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

        [Description(@"""
        Purpose:
        Retrieves node availability failure events for a container app revision.

        Scenario:
        Use this tool to check node availability for a revision.

        Output: Returns tab-separated table data in CSV format. Column headers:
        - PreciseTimeStamp: Timestamp of the event.
        - ReplicaName: Name of the replica where the failure occurred.
        - msg: Log message of the node unavailability.
        """
        )]
        public Task<string> GetNodeAvailabilityFailures(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the revision.")] string revisionName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetNodeAvailabilityFailures", region,
              new Dictionary<string, string> {
                  { "fromDate", fromDate.ToString() },
                  { "toDate", toDate.ToString() },
                  { "containerAppName", containerAppName },
                  { "revisionName", revisionName }
              });
        }

        [Description(@"""
        Purpose:
        Retrieves container app replica count changes over time for a given time frame and application.

        Scenario:
        Use this tool to directly confirm if and when the container app scaled out or in, especially during suspected autoscaling issues. The output will display different time periods with corresponding replica counts.

        Output: Returns tab-separated table data in CSV format. Column headers:
        - StartTime: Start time of the period.
        - EndTime: End time of the period.
        - ReplicaCount: Number of replicas during the period.
        """
)]
        public Task<string> GetContainerAppReplicaCountChanges(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("App name.")] string appName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppReplicaCountChanges", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "appName", appName },
                    { "region", region }
                });
        }

        [Description(@"""
        Purpose:
        Generates a dashboard link for customer issues related to container app revisions.

        Scenario:
        Use this tool to generate a dashboard link for revision-related investigations.

        Output:
        Returns a string containing the dashboard URL.
        - DashboardUrl: Direct URL to the dashboard for the specified revision.
        """
        )]
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

        [Description("""
        Purpose:
        Retrieve the base information, configuration, and state for an Azure Container App or an Azure Container Apps job.

        Scenario:
        Use this tool when you need to search for a specific container app or job and gather its basic details.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - Region
        - ContainerAppName
        - ResourceType
        - ResourceGroup
        - Subscription
        - ManagedEnvironmentId
        - managedClusterName
        - createdTimeUtc
        - provisioningState: Current provisioning state
        - workloadProfileType: Type of workload profile
        """
        )]
        public async Task<string> GetContainerAppInformation(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            // We use All("ContainerAppDBState") in the query, so if the region is not specified, we can default to an arbitrary region.
            string kustoClientRegion = string.IsNullOrEmpty(region)
                ? "centralus"
                : region;

            string containerApps = await _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerApp", kustoClientRegion,
                new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "region", region },
                        { "containerAppName", containerAppName },
                        { "resourceGroupName", resourceGroupName },
                        { "subscriptionId", subscriptionId }
            });
            return containerApps;
        }
    }
}
