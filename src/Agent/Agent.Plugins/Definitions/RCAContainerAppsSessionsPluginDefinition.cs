// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.KustoPlugin;
using Agent.Plugins.Services.Interfaces;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppsSessionsPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public RCAContainerAppsSessionsPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description("""
Purpose:
Retrieves session pool information for a specific session pool within a time range.

Scenario:
Use this tool to get the latest configuration and state of a session pool for troubleshooting or analysis.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- sessionPoolName: Name of the session pool
- PreciseTimeStamp: Timestamp of the latest record
- containerType: ContainerType of session pool
- customContainerTemplate: Template details for custom containers
- dynamicPoolConfigurationJsonString: JSON string with dynamic pool configuration
- kubeEnvironment: Name of the containerapp environment
- managedClusterName: Name of the managed cluster
- maxConcurrentSessions: Maximum concurrent sessions allowed
- poolManagementType: Type of pool management
- legionPodPoolName: Name of the legion pod pool
""")]
        public async Task<string> GetSessionPoolInfo(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Azure subscription ID.")] string subscriptionId,
            [Description("Resource group name.")] string resourceGroupName,
            [Description("Session pool name.")] string sessionPoolName)
        {
            // We use AllSession("SessionPoolDBState") in the query, so if the region is not specified, we can default to an arbitrary region.
            string kustoClientRegion = string.IsNullOrEmpty(region)
                ? "centralus"
                : region;

            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetSessionPoolInfo", kustoClientRegion,
             new Dictionary<string, string> {
                 { "fromDate", fromDate.ToString() },
                 { "toDate", toDate.ToString() },
                 { "region", region },
                 { "subscriptionId", subscriptionId },
                 { "resourceGroupName", resourceGroupName },
                 { "sessionPoolName", sessionPoolName }
             }
             );
        }

        [Description("""
Purpose:
Retrieves changes in session pool configuration for a specific session pool within a time range.

Scenario:
Use this tool to track configuration changes and their time intervals when troubleshooting session pool issues or analyzing configuration drift.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- StartTime: Start time of the configuration interval
- EndTime: End time of the configuration interval
- ComponentType: Type of configuration component that changed
- Value: New value of the component
- ChangeStatus: Indicates if a change occurred
- PreviousValue: Previous value before the change
""")]
        public async Task<string> GetChangesInSessionPool(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Azure subscription ID.")] string subscriptionId,
            [Description("Resource group name.")] string resourceGroupName,
            [Description("Session pool name.")] string sessionPoolName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetChangesInSessionPool", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "subscriptionId", subscriptionId },
                        { "resourceGroupName", resourceGroupName },
                        { "sessionPoolName", sessionPoolName }
             });
        }

        [Description("""
Purpose:
Retrieves error logs from session pool create or update operations for a specific session pool within a time range.

Scenario:
Use this tool to investigate errors during session pool creation or updates when troubleshooting deployment or configuration issues.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- PreciseTimeStamp: Timestamp of the log entry
- Level: Log severity level
- message: Log message
- exception: Exception details if present
- poolType: Type of pool involved
- env_dt_traceId: Trace identifier for the event
""")]
        public async Task<string> GetSessionPoolCreateOrUpdateLogs(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Azure subscription ID.")] string subscriptionId,
            [Description("Resource group name.")] string resourceGroupName,
            [Description("Session pool name.")] string sessionPoolName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetSessionPoolCreateOrUpdateLogs", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "subscriptionId", subscriptionId },
                        { "resourceGroupName", resourceGroupName },
                        { "sessionPoolName", sessionPoolName }
             });
        }

        [Description("""
Purpose:
Checks for allocation availability drops in a legion pod pool within a time range.

Scenario:
Use this tool to find periods when allocation rate was less than 100% for a legion pod pool, indicating resource constraints or availability issues.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- PreciseTimeStamp: Time bucket for the allocation check
- SuccessRate: Allocation success rate (percentage) for the time bucket
""")]
        public async Task<string> GetCodeInterpreterSessionLegionPoolAvailability(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Legion pod pool name.")] string legionPodPoolName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCodeInterpreterSessionLegionPoolAvailability", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "legionPodPoolName", legionPodPoolName },
                        { "legionEnvironmentName", region }
             },
             "legion");
        }

        [Description("""
Purpose:
Retrieves details of an allocated pod for a code interpreter session within a time range.

Scenario:
Use this tool to get pod allocation details for a specific session or a random session when investigating session allocation or resource utilization issues.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- PreciseTimeStamp: Timestamp of the allocation event
- identifier: Session identifier
- podName: Name of the allocated pod
- poolType: Type of pool used for allocation
""")]
        public async Task<string> GetCodeInterpreterSessionAllocatedPods(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Azure subscription ID.")] string subscriptionId,
            [Description("Resource group name.")] string resourceGroupName,
            [Description("Session pool name.")] string sessionPoolName,
            [Description("Session identifier. Leave empty to fetch a random session.")] string sessionIdentifier = "")
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCodeInterpreterSessionAllocatedPods", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "subscriptionId", subscriptionId },
                        { "resourceGroupName", resourceGroupName },
                        { "sessionPoolName", sessionPoolName },
                        { "sessionIdentifier", sessionIdentifier }
             });
        }

        [Description("""
Purpose:
Retrieves error logs from code interpreter session execution events for a specific session or a random session within a time range.

Scenario:
Use this tool to investigate errors during session execution when troubleshooting code interpreter failures or session runtime issues.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- PreciseTimeStamp: Timestamp of the log entry
- Tenant: Tenant name
- Level: Log severity level
- message: Log message
- podId: Pod identifier
- exception: Exception details if present
- poolType: Type of pool involved
- env_dt_traceId: Trace identifier for the event
- identifier: Session identifier
- sessionPoolName: Name of the session pool
- podName: Name of the pod
""")]
        public async Task<string> GetCodeInterpreterSessionExecutionEventLogs(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Azure subscription ID.")] string subscriptionId,
            [Description("Resource group name.")] string resourceGroupName,
            [Description("Session pool name.")] string sessionPoolName,
            [Description("Session identifier. Leave empty to fetch a random session.")] string sessionIdentifier = "")
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCodeInterpreterSessionExecutionEventLogs", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "subscriptionId", subscriptionId },
                        { "resourceGroupName", resourceGroupName },
                        { "sessionPoolName", sessionPoolName },
                        { "sessionIdentifier", sessionIdentifier }
             });
        }

        [Description("""
Purpose:
Retrieves error events for a code interpreter session pod by pod name within a time range.

Scenario:
Use this tool to get error events for a specific pod when investigating pod-level issues or runtime failures in code interpreter sessions.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- PreciseTimeStamp: Timestamp of the error event
- LegionComponent: Name of the legion component
- eventId: Event identifier
- Value: Event value or details
- Message: Error message
""")]
        public async Task<string> GetCodeInterpreterSessionPodEventLogs(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Pod name or pod ID.")] string podName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCodeInterpreterSessionPodEventLogs", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "podName", podName }
             },
             "legion");
        }

        [Description("""
Purpose:
Retrieves error logs for a code interpreter session pod by pod name within a time range.

Scenario:
Use this tool to fetch error logs for a specific pod when investigating detailed pod-level failures or container runtime issues.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- PreciseTimeStamp: Timestamp of the log entry
- ManagedClusterName: Name of the managed cluster
- _ContainerGroupId: Container group identifier (pod name)
- Log: Log message
- Stream: Log stream name
- _Region: Azure region
- Tenant: Tenant name
""")]
        public async Task<string> GetCodeInterpreterSessionPodLogs(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Pod name or pod ID.")] string podName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCodeInterpreterSessionPodLogs", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "podName", podName }
             });
        }

        [Description("""
Purpose:
Retrieves error logs from custom container session activator for a specific session pool and managed environment within a time range.

Scenario:
Use this tool to investigate errors in pod allocation for new session requests when troubleshooting custom container session activation failures.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- PreciseTimeStamp: Timestamp of the log entry
- LogMessage: Log message
- level: Log severity level
- logger: Logger name
- stacktrace: Stack trace if present
- error: Error details
- Url: URL involved in the request
- caller: Caller information
- _ContainerName: Name of the container
- LegionRCPEndpoint: Legion RCP endpoint
- Payload: Payload details
- StatusCode: Status code of the request
- SessionIdentifier: Session identifier
- RequestId: Request identifier
- SessionPoolName: Name of the session pool
""")]
        public async Task<string> GetCustomContainerSessionActivatorLogs(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Azure subscription ID.")] string subscriptionId,
            [Description("Resource group name.")] string resourceGroupName,
            [Description("Session pool name.")] string sessionPoolName,
            [Description("Managed environment name.")] string managedEnvironmentName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomContainerSessionActivatorLogs", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "subscriptionId", subscriptionId },
                        { "resourceGroupName", resourceGroupName },
                        { "sessionPoolName", sessionPoolName },
                        { "managedEnvironmentName", managedEnvironmentName }
             });
        }

        [Description("""
Purpose:
Retrieves all failed envoy requests for a custom container session within a time range.

Scenario:
Use this tool to identify failed requests and their response codes when troubleshooting network connectivity or routing issues in custom container sessions.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- PreciseTimeStamp: Timestamp of the request
- Method: HTTP method used
- Path: Request path
- Status: HTTP status code
- UpstreamHost: Upstream host for the request
- ResponseCodeDetails: Envoy response code details
""")]
        public async Task<string> GetCustomContainerSessionEnvoyRequests(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Session pool name.")] string sessionPoolName,
            [Description("Managed cluster name.")] string managedClusterName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomContainerSessionEnvoyRequests", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "sessionPoolName", sessionPoolName },
                        { "managedClusterName", managedClusterName },
                        { "region", region }
             });
        }

        [Description("""
Purpose:
Retrieves the status of a custom container session legion pool for a specific session pool within a time range.

Scenario:
Use this tool to get the number of pods in different states (ready, pending, allocated, inactive) when analyzing pool capacity and resource allocation.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- PreciseTimeStamp: Timestamp of the status record
- expected: Expected number of pods in the pool
- ready: Number of pods ready
- totalPending: Total number of pending pods
- healthyPending: Number of healthy pending pods
- crashingPending: Number of crashing pending pods
- imagePullFailingPending: Number of pods failing due to image pull errors
- allocated: Number of allocated pods
- inactive: Number of inactive pods
""")]
        public async Task<string> GetCustomContainerSessionLegionPoolStatus(
          [Description("Azure region.")] string region,
          [Description("Start time of the query.")] DateTime fromDate,
          [Description("End time of the query.")] DateTime toDate,
          [Description("Azure subscription ID.")] string subscriptionId,
          [Description("Resource group name.")] string resourceGroupName,
          [Description("Session pool name.")] string sessionPoolName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomContainerSessionLegionPoolStatus", region,
             new Dictionary<string, string> {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "subscriptionId", subscriptionId },
                        { "resourceGroupName", resourceGroupName },
                        { "sessionPoolName", sessionPoolName },
                        { "legionEnvironmentName", region }
             },
             "legion");
        }

    }
}
