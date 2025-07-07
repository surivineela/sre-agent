using System.ComponentModel;
using Agent.Data.DatabaseClients.GraphDbClient;
using Microsoft.SemanticKernel;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppCustomerLogsPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public RCAContainerAppCustomerLogsPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description(@"""
Retrieves log configuration changes for a managed environment within a time range.
Use this tool to check log destination changes and dynamic JSON column settings.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- StartTime: Start time of the configuration interval.
- EndTime: End time of the configuration interval.
- Value: Log destination after the change.
- ChangeStatus: Indicates if the log destination changed.
- PreviousValue: Log destination before the change.
- hasDynamicJsonColumns: Indicates if dynamic JSON columns are present.
"""
)]
        public Task<string> GetLogConfiguration(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Customer subscription ID of the managed environment.")] Guid customerSubscriptionId,
            [Description("Name of the managed environment.")] string managedEnvironmentName,
            [Description("Name of the managed cluster. Use empty string if not available.")] string managedClusterName
        )
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetCustomerLogConfiguration", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "customerSubscriptionId", customerSubscriptionId.ToString() },
                { "managedEnvironmentName", managedEnvironmentName },
                { "managedClusterName", managedClusterName }
            });
        }

        [Description(@"""
Retrieves event processor errors for a managed environment within a time range.
Use this tool to find warnings and errors from event processor logs.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- Type: Type of event (e.g., Warning, Error, Normal).
- msg: Event message.
- Reason: Reason for the event.
- count: Number of occurrences (for warnings/errors).
"""
)]
        public Task<string> GetEventProcessorErrors(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the container app or job. Use empty string if not available.")] string containerAppOrJobName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEventProcessorErrors", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "containerAppOrJobName", containerAppOrJobName }
            });
        }

        [Description(@"""
Retrieves leader election events for event processors in a managed environment within a time range.
Use this tool to check if leader election events occurred.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Timestamp of the leader election event.
- msg: Event message.
"""
)]
        public Task<string> GetEventProcessorLeaderElectionEvents(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEventProcessorLeaderElectionEvents", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
            });
        }

        [Description(@"""
Retrieves apps and jobs volume for a managed environment within a time range.
Use this tool to get the volume of apps and jobs in the environment.
Output: Returns tab-separated table data in CSV format. The first line contains column headers.
"""
)]
        public Task<string> GetAppOrJobVolumeForEnv(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetAppOrJobVolumeForEnv", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
            });
        }

        [Description(@"""
Retrieves event processor pods for a managed environment within a time range.
Use this tool to list event processor pods and their node assignments.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- StartTime: Start time of the pod's activity.
- EndTime: End time of the pod's activity.
- Node: Node name where the pod is running.
- PodName: Name of the pod.
"""
)]
        public Task<string> GetEventProcessorPods(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodsWithPrefix", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "podNamePrefix", "k8se-event-processor" }
            });
        }

        [Description(@"""
Retrieves log processor pods for a managed environment within a time range.
Use this tool to list log processor pods and their node assignments.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- StartTime: Start time of the pod's activity.
- EndTime: End time of the pod's activity.
- Node: Node name where the pod is running.
- PodName: Name of the pod.
"""
)]
        public Task<string> GetLogProcessorPods(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodsWithPrefix", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "podNamePrefix", "k8se-log-processor" }
            });
        }

        [Description(@"""
Retrieves event processor pod status for a managed environment within a time range.
Use this tool to get health and status of event processor pods.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- StartTime: Start time of the pod's activity.
- EndTime: End time of the pod's activity.
- PodName: Name of the pod.
- NodeName: Name of the node.
- PodStatus: Status of the pod.
- Health: Health status (Healthy/Degraded).
- restartCount: Number of restarts.
- ContainerName: Name of the container (for failures).
- ContainerState: State of the container (Ready/Not Ready).
- ContainerImage: Image used by the container.
"""
)]
        public Task<string> GetEventProcessorPodStatus(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "podNamePrefix", "k8se-event-processor" },
                { "podNamespace", "k8se-system" }
            });
        }

        [Description(@"""
Retrieves log processor pod status for a managed environment within a time range.
Use this tool to get health and status of log processor pods.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- StartTime: Start time of the pod's activity.
- EndTime: End time of the pod's activity.
- PodName: Name of the pod.
- NodeName: Name of the node.
- PodStatus: Status of the pod.
- Health: Health status (Healthy/Degraded).
- restartCount: Number of restarts.
- ContainerName: Name of the container (for failures).
- ContainerState: State of the container (Ready/Not Ready).
- ContainerImage: Image used by the container.
"""
)]
        public Task<string> GetLogProcessorPodStatus(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "podNamePrefix", "k8se-log-processor" },
                { "podNamespace", "k8se-system" }
            });
        }

        [Description(@"""
Retrieves the workload profile type for a container app or job within a time range.
Use this tool to get the workload profile type.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- workloadProfileType: Type of workload profile.
"""
)]
        public Task<string> GetContainerAppWorkloadProfile(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app or job.")] string containerAppOrJobName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppWorkloadProfile", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "containerAppOrJobName", containerAppOrJobName }
            });
        }

        [Description(@"""
Retrieves input pressure metrics for log processor in a managed cluster within a time range.
Use this tool to monitor total input records to log processor.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Timestamp of the metric.
- totalCount: Total input records.
- VMNodeWhereMetricCaptured: Node or VMSS where metric was captured.
- PodName: Name of the pod.
"""
)]
        public Task<string> GetInputPressureOnLogProcessor(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "metricName", "fluentbit_input_records_total" },
                { "duration", GetDuration(fromDate, toDate) },
                { "threshold", "0" }
            });
        }

        [Description(@"""
Retrieves memory pressure metrics for fluentbit in a managed cluster within a time range.
Use this tool to monitor input storage memory usage by fluentbit.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Timestamp of the metric.
- totalCount: Total memory used (bytes).
- VMNodeWhereMetricCaptured: Node or VMSS where metric was captured.
- PodName: Name of the pod.
"""
)]
        public Task<string> GetMemoryPressureOnFluentbit(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region.NormalizeLocation(),
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "metricName", "fluentbit_input_storage_memory_bytes" },
                { "duration", GetDuration(fromDate, toDate) },
                { "threshold", "0" }
            });
        }

        [Description(@"""
Retrieves output count metrics for fluentbit in a managed cluster within a time range.
Use this tool to monitor total output records processed by fluentbit.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Timestamp of the metric.
- totalCount: Total output records processed.
- VMNodeWhereMetricCaptured: Node or VMSS where metric was captured.
- PodName: Name of the pod.
"""
)]
        public Task<string> GetFluentbitOutputCount(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region.NormalizeLocation(),
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "metricName", "fluentbit_output_proc_records_total" },
                { "duration", GetDuration(fromDate, toDate) },
                { "threshold", "0" }
            });
        }

        [Description(@"""
Retrieves buffer pressure metrics for fluentbit in a managed cluster within a time range.
Use this tool to monitor input storage buffer overflows for fluentbit.
Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
- PreciseTimeStamp: Timestamp of the metric.
- totalCount: Total buffer overflows.
- VMNodeWhereMetricCaptured: Node or VMSS where metric was captured.
- PodName: Name of the pod.
"""
)]
        public Task<string> GetFluentbitBufferPressure(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetGenericMetricCountData", region.NormalizeLocation(),
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "metricName", "fluentbit_input_storage_overlimit" },
                { "duration", GetDuration(fromDate, toDate) },
                { "threshold", "0" }
            });
        }

        [Description(@"""
Retrieves output errors for fluentbit for a container app or job in a managed cluster within a time range.
Use this tool to monitor output errors experienced by fluentbit.
Output: Returns tab-separated table data in CSV format. The first line contains column headers.
"""
)]
        public Task<string> GetFluentbitOutputErrors(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetFluentBitOutputErrorsForApp", region,
            new Dictionary<string, string> {
                { "region", region.ToString() },
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "metricName", "fluentbit_output_errors_total" }
            });
        }

        private static string GetDuration(DateTime fromDate, DateTime toDate)
        {
            var totalHours = (toDate - fromDate).TotalHours;
            var totalDays = (toDate - fromDate).TotalDays;
            // Use the lowest frequency possible for the given range
            if (totalDays > 5)
            {
                return "1d";
            }
            if (totalHours > 24)
            {
                return "1h";
            }
            return "1m";
        }
    }
}
