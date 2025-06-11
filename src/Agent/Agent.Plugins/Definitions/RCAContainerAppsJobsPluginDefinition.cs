using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Kusto;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class RCAContainerAppsJobsPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPluginChat;

        public RCAContainerAppsJobsPluginDefinition(IKustoPluginChat kustoPluginChat)
        {
            _kustoPluginChat = kustoPluginChat;
        }

        [Description(
            """
            Retrieve the Container Apps job definition (spec) for a given Container App Job
            Projects:
              - Timestamp: Timestamp of the job definition. More than 1 row indicates change in job defintion(spec).
              - Configuration: Configuration details for th job, like trigger type, retries, job deadlines, completion times
                                parallelism for the job, container registry, assigned identity etc details.
              - Template: Job template containing job containers deatails, cpu, memory resource details.
              - Labels: Labels for the job. It has the managed environment name and workloadprofile name for the job.
              - Status: Status of the container app Job. It has jobRunningState and jobProvisioningState.
                               Possible values are for jobRunningState: Running, Suspended.
                               Possible values for jobProvisioningState: Provisioned, Failed.
            """)]
        public Task<string> GetJobDefinition(
            [Description("The name of the Container App Job")] string containerAppJobName,
            [Description("The Azure region")] string region,
            [Description("The resource group of the Container App Job")] string cappResourceGroup,
            [Description("The subscription ID of the Container App Job")] string cappSubscription,
            [Description("Name of the managed cluster")] string cappClusterName,
            [Description("The start of the time range for the query")] DateTime queryFrom,
            [Description("The end of the time range for the query")] DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "cappName", containerAppJobName },
                { "cappResourceGroup", cappResourceGroup },
                { "cappSubscription", cappSubscription },
                { "cappClusterName", cappClusterName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetJobDefinition", region, args);
        }

        [Description(
            """
            Get the job execution's final status for a Container App Job. It contains detailed status of the given
            job execution, whether succeeded or failed, if failed, failure reason and message details in JobExecutionStatusDetails column.
            Projects:
              - PreciseTimeStamp: Precise timestamp of the event.
              - JobExecutionName: Name of the job execution.
              - JobExecutionStatus: Status of the job execution, ex: Succeeded, Failed.
              - JobExecutionStatusDetails: Detailed status of the job execution, if failed, it has reason for failure, message etc useful details.
            """)]
        public Task<string> GetJobExecutionFinalStatus(
            [Description("The Azure region")] string region,
            [Description("Name of the managed cluster")] string managedClusterName,
            [Description("Name of the jobExecutionName")] string jobExecutionName,
            [Description("The start of the time range for the query")] DateTime queryFrom,
            [Description("The end of the time range for the query")] DateTime queryTo)
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

        [Description(
            """
            Get full lifecycle events for a specific Container App Job execution from EventProcessorEvents.
            Projects:
              - PreciseTimeStamp: Precise timestamp of the event.
              - msg: Log message of the event.
              - Reason: Reason for the event.
              - Count: Count of the event.
              - Type: Type of the event, ex: Warning, Normal, Error etc.
            """)]
        public Task<string> GetJobExecutionEvents(
            [Description("The Azure region")] string region,
            [Description("The name of the job execution")] string jobExecutionName,
            [Description("Name of the managed cluster")] string managedClusterName,
            [Description("The start of the time range for the query (UTC datetime)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime)")] DateTime queryTo)
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

        [Description(
            """
            Gets all error events for all job executions of a given ContainerApp Job.
            """)]
        public Task<string> GetAllJobExecutionsErrorEvents(
            [Description("The Azure region")] string region,
            [Description("Name of the managed cluster")] string managedClusterName,
            [Description("Name of the container app job")] string containerAppJobName,
            [Description("The start of the time range for the query (UTC datetime)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime)")] DateTime queryTo)
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

        [Description(
            """
            Gets the final status for all job executions of a given ContainerApp Job.
            """)]
        public Task<string> GetAllJobExecutionsFinalStatus(
            [Description("The Azure region")] string region,
            [Description("Name of the managed cluster")] string managedClusterName,
            [Description("Name of the container app job")] string containerAppJobName,
            [Description("The start of the time range for the query (UTC datetime)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime)")] DateTime queryTo)
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

        [Description(
            """
            Retrieve KEDA events for job scaled jobs.
            Projects:
                - Timestamp: Event timestamp
                - Level: Log level
                - Logger: KEDA component logger
                - Message: KEDA event message
                - ScalerType: Type of scaler used
                - JobName: Associated job name
            """)]
        public Task<string> GetKedaEventsForJobScaledJobs(
            [Description("The Azure region")] string region,
            [Description("Name of the managed cluster")] string managedClusterName,
            [Description("The name of the Container App Job")] string containerAppJobName,
            [Description("The start of the time range for the query (UTC datetime)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime)")] DateTime queryTo)
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

        [Description(
            """
            Retrieve Legion VK events for jobs running on Consumption V2 workload profile.
            Projects:
                - Timestamp: Event timestamp  
                - Level: Log level
                - Message: Legion VK event message
                - PodName: Associated pod name
                - JobName: Associated job name
                - Phase: Pod lifecycle phase
                - Reason: Event reason
            """)]
        public Task<string> GetLegionVKEventsForJobsRunningConsumptionV2(
            [Description("The Azure region")] string region,
            [Description("Name of the managed cluster")] string managedClusterName,
            [Description("The name of the job execution")] string jobExecutionName,
            [Description("The start of the time range for the query (UTC datetime)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime)")] DateTime queryTo)
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
    }
}
