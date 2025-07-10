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
        private readonly IKustoPluginChat _kustoPlugin;
        private readonly IKustoDashboardPlugin _kustoDashboardPlugin;

        public RCAContainerAppRevisionPluginDefinition(IKustoPluginChat kustoPlugin, IKustoDashboardPlugin kustoDashboardPlugin)
        {
            _kustoPlugin = kustoPlugin;
            _kustoDashboardPlugin = kustoDashboardPlugin;
        }

        [Description(@"""
Retrieves active revisions with configuration, workload profile, scaling settings, and app status.
Use this tool to list all active revisions and their configuration details for a container app.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- Name: Revision name.
- ManagedClusterName: Cluster name.
- ContainerAppName: App name.
- Namespace: Kubernetes namespace.
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
- HttpScalingRuleConcurrentRequests
- ObservedGeneration
- RevisionProvisioningState
- RevisionHealthStatus
- RevisionRunningState
- AppReadyForTrafficState
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

        [Description(@"""
Retrieves events that is about HTTP scaler for a container app.
Use this tool to diagnose scaling issues when a container app has HTTP scaler.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: When the scaling event occurred.
- EnvironmentName: Name of the cluster hosting the container app.
- msg: Detailed message describing the scaling activity or failure reason.
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

        [Description(@"""
Retrieves KEDA Operator events related to scaling actions or failures for a container app.
Use this tool to review KEDA Operator events and diagnose scaling issues.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Log timestamp.
- Log: Operator event message.
- KedaVersion: Current KEDA version.
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

        [Description(@"""
Retrieves a direct App Service Insights (ASI) page URL for a specific revision.
Use this tool to get a diagnostic insights link for a container app revision over a specified time range.
Output: Returns a string containing the ASI page URL.
- ASIPageUrl: Direct URL to the ASI diagnostics page for the specified revision.
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
            var clusterName = await _kustoPlugin.ExecuteFunctionAsync("GetManagedClusterName", region,
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

        [Description(@"""
Retrieves replica counts and HTTP request counts for a revision over time.
Use this tool to diagnose scaling issues and detect periods where replicas exist but no traffic is received.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- Timestamp: Timestamp for the data point.
- ReplicaCount: Number of active replicas at the timestamp.
- Status: HTTP response status (e.g., 200, 503).
- Requests: Number of HTTP requests for that status.
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

        [Description(@"""
Retrieves the status of a container app revision for a given time range.
Use this tool to get the provisioning and running status of a revision.
Output: Returns tab-separated table data in CSV format. The first line contains column headers:
- Status and state columns as defined in the revision status query.
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

        [Description(@"""
Retrieves the replica count of a revision for a given time range.
Use this tool to get the number of replicas for a revision over time.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- Timestamp: Timestamp of the data point.
- Revision: Name of the revision.
- Max: Maximum replica count at the timestamp.
- appArmId: ARM ID of the app.
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

            var result = await _kustoPlugin.ExecuteKustoQuery(region, query);

            return result.Result;
        }

        [Description(@"""
Retrieves active sessions (start/stop/running changes) for a revision.
Use this tool to get session state changes and health for a revision.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- StartTime: Session start timestamp.
- EndTime: Session end timestamp.
- RevisionName: The revision name.
- State: The running state (e.g., Running, Stopped).
- HealthStatus: Health status derived from state.
"""
)]
        public Task<string> GetActiveRevisionSessions(
            [Description("Azure region.")] string region,
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

        [Description(@"""
Purpose:
Retrieves the maximum and minimum ratios of the current to target metric values of different scaler in HPA  (Horizontal Pod Autoscaler)

When to use:
Use this tool to check whether HPA triggers the scale-up or scale-down when analysis scaling issue.
Please mention if HPA triggers the scale-up or scale-down when use this tool

Output:
Returns tab-separated table data in CSV format. The first line contains these column headers:
- metricName: Name of the metric.
- MaxRatio: Maximum ratio of the current value to the target value of the metric.
- MinRatio: Minimum ratio of the current value to the target value of the metric.
- TriggeredScaleUp: scaling up event is triggered
- TriggeredScaleDown: A scaling down event may be triggered. However, this should be verified against the replica count, because scaling down will not be triggered if the number of replicas is already low (at or near the minimum allowed).
- TargetValue: the target value to trigger scaling
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
Retrieves revision spec changes for a given revision and time range.
Use this tool to get changes in the revision specification over time.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
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
Retrieves all ARM (Azure Resource Manager) operations for the container app.
Use this tool to get ARM operation events and their health status for a container app.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
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

        [Description(@"""
Retrieves EventProcessor events for a revision where no replica is associated.
Use this tool to get EventProcessor events without replica association for a revision.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Timestamp of event.
- RevisionName: Name of the revision.
- Reason: Reason for the event.
- msg: Additional event message details.
"""
)]
        public Task<string> GetEventProcessorEventsWithoutReplica(
            [Description("Azure region.")] string region,
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

        [Description(@"""
Retrieves the latest pod heartbeat status for a revision.
Use this tool to get the latest pod status and heartbeat timestamp for a revision.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
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
Retrieves internal EventProcessor events for a specific pod inside a revision.
Use this tool to get internal EventProcessor events for a pod in a revision.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Event timestamp.
- Type: Type of event (Normal/Error).
- msg: Event message.
- Reason: Short reason description.
- Count: Number of occurrences.
- EventSource: Event origin.
- ReplicaName: Name of the replica.
- RevisionName: Name of the revision.
- level: Info or Error.
"""
)]
        public Task<string> GetInternalEventProcessorEventsForPod(
            [Description("Azure region.")] string region,
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

        [Description(@"""
Retrieves Legion errors for a given revision.
Use this tool to get Legion error events for a revision.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
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

        [Description(@"""
Retrieves readiness, liveness, or startup probe failures for a container app revision.
Use this tool to get health probe failure events for a revision.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- msg: Log message of the probe failure.
- ReplicaName: Name of the replica where the failure occurred.
- Count: Number of times the probe failed with the same message consecutively.
"""
)]
        public Task<string> GetHealthProbeFailures(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the revision.")] string revisionName,
            [Description("Sampling options for the query.")] SamplingOptions sampling)
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
Retrieves the latest health probe settings for a container app.
Use this tool to get the health probe configuration for a container app.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
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
Retrieves node availability failure events for a container app revision.
Use this tool to get node unavailability events for a revision.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
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
Retrieves container app replica count changes over time for a given time frame and application.
Use this tool to directly confirm if and when the container app scaled out or in, especially during suspected autoscaling issue. The output will display different time periods with corresponding replica counts.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
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
Generates a dashboard link for customer issues related to container app revisions.
Use this tool to generate a dashboard link for revision-related investigations.
Output: Returns a string containing the dashboard URL.
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
        Retrieve the base information, configuration, and state for an Azure Container App or an Azure Container Apps job.
        Use this tool when you need to search for a specific container app or job and gather its basic details.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
            
        Tool Output:
        - Region: Azure region where the container app is hosted.
        - ContainerAppName: Name of the container app.
        - ResourceType: Resource type of the container app (e.g., containerApp or job).
        - ResourceGroup: Name of the resource group containing the container app.
        - Subscription: Azure subscription ID.
        - ManagedEnvironmentId: Managed environment Resource ID for the container app.
        - managedClusterName: Name of the managed cluster.
        - createdTimeUtc: Time when the container app was created.
        - provisioningState: Current provisioning state of the container app.
        - workloadProfileType: Type of workload profile for the container app.
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
