using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.KustoPlugin;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppsJobsPluginDefinition
    {
        private readonly IKustoPlugin _kustoPluginChat;

        public RCAContainerAppsJobsPluginDefinition(IKustoPlugin kustoPluginChat)
        {
            _kustoPluginChat = kustoPluginChat;
        }

        [Description(@"""
        Purpose:
        Retrieves the Container Apps job definition (spec) for a given Container App Job.

        Scenario:
        Use this tool to get the job definition, configuration, template, labels, and status for a container app job.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - Timestamp: Timestamp of the job definition
        - Configuration: Configuration details for the job
        - Template: Job template with container and resource details
        - Labels: Labels for the job, including environment and workload profile
        - Status: Status of the container app job
        """
        )]
        public Task<string> GetJobDefinition(
            [Description("Name of the Container App Job.")] string containerAppJobName,
            [Description("Azure region.")] AzureRegion region,
            [Description("Name of the managed cluster.")] string cappClusterName,
            [Description("Start of the time range for the query.")] DateTime queryFrom,
            [Description("End of the time range for the query.")] DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "cappName", containerAppJobName },
                { "cappClusterName", cappClusterName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetJobDefinition", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves the final status for a specific job execution of a Container App Job.

        Scenario:
        Use this tool to get the final status and details for a job execution.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - PreciseTimeStamp: Timestamp of the event
        - JobExecutionName: Name of the job execution
        - JobExecutionStatus: Status of the job execution (e.g., Succeeded, Failed)
        - JobExecutionStatusDetails: Detailed status or failure reason
        """
        )]
        public Task<string> GetJobExecutionFinalStatus(
            [Description("Azure region.")] AzureRegion region,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the job execution.")] string jobExecutionName,
            [Description("Start of the time range for the query.")] DateTime queryFrom,
            [Description("End of the time range for the query.")] DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "managedClusterName", managedClusterName },
                { "jobExecutionName", jobExecutionName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetJobExecutionFinalStatus", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves full lifecycle events for a specific Container App Job execution.

        Scenario:
        Use this tool to get all EventProcessorEvents for a job execution.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - PreciseTimeStamp: Timestamp of the event
        - msg: Log message of the event
        - Reason: Reason for the event
        - Count: Count of the event
        - Type: Type of the event (e.g., Warning, Normal, Error)
        """
        )]
        public Task<string> GetJobExecutionEvents(
            [Description("Azure region.")] AzureRegion region,
            [Description("Name of the job execution.")] string jobExecutionName,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Start of the time range for the query (UTC datetime).")] DateTime queryFrom,
            [Description("End of the time range for the query (UTC datetime).")] DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "jobExecutionName", jobExecutionName },
                { "managedClusterName", managedClusterName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetJobExecutionEvents", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves all error events for all job executions of a given Container App Job.

        Scenario:
        Use this tool to get error events for all executions of a container app job.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - msg: Log message of the error event
        - Reason: Reason for the error event
        - Count: Number of occurrences
        """
        )]
        public Task<string> GetAllJobExecutionsErrorEvents(
            [Description("Azure region.")] AzureRegion region,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the container app job.")] string containerAppJobName,
            [Description("Start of the time range for the query (UTC datetime).")] DateTime queryFrom,
            [Description("End of the time range for the query (UTC datetime).")] DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "managedClusterName", managedClusterName },
                { "containerAppJobName", containerAppJobName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetAllJobExecutionsErrorEvents", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves the final status for all job executions of a given Container App Job.

        Scenario:
        Use this tool to get the final status for all executions of a container app job.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - JobExecutionName: Name of the job execution
        - JobExecutionStatus: Status of the job execution
        - JobExecutionStatusDetails: Detailed status or failure reason
        """
        )]
        public Task<string> GetAllJobExecutionsFinalStatus(
            [Description("Azure region.")] AzureRegion region,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the container app job.")] string containerAppJobName,
            [Description("Start of the time range for the query (UTC datetime).")] DateTime queryFrom,
            [Description("End of the time range for the query (UTC datetime).")] DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "managedClusterName", managedClusterName },
                { "containerAppJobName", containerAppJobName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetAllJobExecutionsFinalStatus", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves KEDA events for job scaled jobs.

        Scenario:
        Use this tool to get KEDA scaler events for jobs.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - PreciseTimeStamp: Event timestamp
        - LogInfo: Log information array
        - LogLevel: Log level
        - LogCategory: KEDA component logger
        - _ContainerGroupName: Container group name
        """
        )]
        public Task<string> GetKedaEventsForJobScaledJobs(
            [Description("Azure region.")] AzureRegion region,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the Container App Job.")] string containerAppJobName,
            [Description("Start of the time range for the query (UTC datetime).")] DateTime queryFrom,
            [Description("End of the time range for the query (UTC datetime).")] DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "managedClusterName", managedClusterName },
                { "containerAppJobName", containerAppJobName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("KedaEventsJobScaledJobs", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves Legion VK events for jobs running on Consumption V2 workload profile.

        Scenario:
        Use this tool to get Legion VK events for jobs running on Consumption V2.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - phase: Pod lifecycle phase
        - msg: Legion VK event message
        - error: Error message if present
        - RequestMethod: HTTP request method
        - ResponseHttpStatusCode: HTTP response status code
        """
        )]
        public Task<string> GetLegionVKEventsForJobsRunningConsumptionV2(
            [Description("Azure region.")] AzureRegion region,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the job execution.")] string jobExecutionName,
            [Description("Start of the time range for the query (UTC datetime).")] DateTime queryFrom,
            [Description("End of the time range for the query (UTC datetime).")] DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "jobExecutionName", jobExecutionName },
                { "managedClusterName", managedClusterName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("LegionVKEventsForJobsRunningConsumptionV2", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves container app job execution errors from Legion System Logs for consumption workload profile jobs.

        Scenario:
        Use this tool to get error details for job executions from Legion System Logs.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - Message: Error message
        - Value: Error value
        - count_: Error count
        """
        )]
        public Task<string> GetLegionSystemLogsForJobExecutionErrors(
            [Description("Azure region.")] AzureRegion region,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the container app job.")] string containerAppJobName,
            [Description("Name of the specific job execution, or empty string.")] string jobExecutionName,
            [Description("Start of the time range for the query (UTC datetime).")] DateTime queryFrom,
            [Description("End of the time range for the query (UTC datetime).")] DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "managedClusterName", managedClusterName },
                { "containerAppJobName", containerAppJobName },
                { "jobExecutionName", jobExecutionName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync(
                "GetLegionSystemLogsForJobExecutionErrors",
                region,
                args,
                groupName: "Legion");
        }

        [Description(@"""
        Purpose:
        Retrieves the Azure Service Insights (ASI) page link for the specified Container App Job.

        Scenario:
        Use this tool to get a direct link to the ASI portal for a container app job over a specified time range.

        Output:
        Returns a string containing the ASI page URL:
        - ASIPageUrl: Direct URL to the ASI diagnostics page for the specified job
        """
        )]
        public Task<string> GetASIPageForContainerAppJob(
           [Description("Azure region.")] AzureRegion region,
           [Description("Start of the time range for the query (UTC datetime).")] DateTime fromDate,
           [Description("End of the time range for the query (UTC datetime).")] DateTime toDate,
           [Description("Name of the Container App Job.")] string containerAppName,
           [Description("Resource group of the Container App Job.")] string resourceGroupName,
           [Description("Subscription ID of the Container App Job.")] string subscriptionId)
        {
            var basePath = "/services/ACA Azure Container Apps/pages/Container App";
            #pragma warning disable SYSLIB0013
            var cleanPath = Uri.EscapeUriString(basePath); // DO NOT CHANGE TO EscapeDataString
            #pragma warning restore SYSLIB0013

            var query =
                $"cappName={Uri.EscapeDataString(containerAppName.Trim())}" +
                $"&cappResourceGroup={Uri.EscapeDataString(resourceGroupName.Trim())}" +
                $"&cappSubscription={Uri.EscapeDataString(subscriptionId.Trim())}" +
                $"&location={Uri.EscapeDataString(region.ToNormalizedString())}" +
                $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}" +
                $"&globalTo={Uri.EscapeDataString(toDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"))}";

            var asiUri = $"https://asi.azure.ms{cleanPath}?{query}";

            return Task.FromResult($"ASI Page for container app job {asiUri}");
        }

        [Description(@"""
        Purpose:
        Checks if any pods in a specified job within a managed cluster have CPU usage exceeding a given threshold during a time window.

        Scenario:
        Use this tool when jobs are experiencing performance degradation to determine if CPU usage is elevated during the specified time range, which may contribute to these issues.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - hasData: true if any pod's CPU usage exceeded the threshold, false otherwise
        """
        )]
        public Task<string> GetJobCPUUsageExceedsThreshold(
            [Description("Azure region in lower case. Example: 'westeurope'.")] AzureRegion region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the job.")] string jobName,
            [Description("Threshold as a percentage to check if usage equals or exceeds. Example: '90'.")] string threshold)
        {
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetJobCPUUsageExceedsThreshold", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "jobName", jobName },
                    { "threshold", threshold }
                });
        }

        [Description(@"""
        Purpose:
        Checks if any pods in a specified job within a managed cluster have memory usage exceeding a given threshold during a time window.

        Scenario:
        Use this tool when jobs are crashing or experiencing performance degradation to determine if pod memory usage is elevated during the specified time range, which may contribute to these issues.

        Output:
        Returns tab-separated table data in CSV format. Column headers:
        - hasData: true if any pod's memory usage exceeded the threshold, false otherwise
        """
        )]
        public Task<string> GetJobMemoryUsageExceedsThreshold(
            [Description("Azure region in lower case. Example: 'westeurope'.")] AzureRegion region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the job.")] string jobName,
            [Description("Threshold as a percentage to check if usage equals or exceeds. Example: '90'.")] string threshold)
        {
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetJobMemoryUsageExceedsThreshold", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "jobName", jobName },
                    { "threshold", threshold }
                });
        }


        [Description("""
        Purpose:
        Retrieves pod containers termination states for the specified job in the given time range.

        Scenario:
        Use this tool when job pods are crashing or terminating unexpectedly, to analyze termination states of pod containers.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - StartTime: Start time of the Container App pod status
        - EndTime: End time of the Container App pod status
        - NodeName: Name of the node where the job pod is running
        - PodName: Name of the job pod
        - ContainerName: Pod container name
        - ContainerState: State of the pod container (Ready or NotReady)
        - ContainerTerminationExitCode: Exit code of the container termination
        - ContainerTerminationReason: Reason for the container termination
        """
        )]
        public Task<string> GetJobPodContainersTerminationState(
            [Description("Azure region in lower case. Example: 'westeurope'.")] AzureRegion region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the job.")] string jobName)
            {
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetPodContainersTerminationState", region,
            new Dictionary<string, string>
            {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "podNamePrefix", jobName },
                { "podNamespace", "k8se-apps" }
            });
        }

    }
}
