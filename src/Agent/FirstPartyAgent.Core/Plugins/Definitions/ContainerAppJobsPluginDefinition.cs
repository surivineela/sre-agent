// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.SemanticKernel;
using System.ComponentModel;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Plugins.Interfaces; // Assuming KernelFunctionNames.ACA might be extended here or this is for other constants

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class ContainerAppJobsPluginDefinition
    {
        private readonly IContainerAppJobsPlugin _plugin;

        public ContainerAppJobsPluginDefinition(IContainerAppJobsPlugin plugin)
        {
            _plugin = plugin;
        }

        [KernelFunction(KernelFunctionNames.Jobs.GetJobDefinition)]
        [Description(@"Retrieve the Container Apps job definition (spec) for a given Container App Job
Projects:
  - Timestamp: Timestamp of the job definition. More than 1 row indicates change in job defintion(spec).
  - Configuration: Configuration details for th job, like trigger type, retries, job deadlines, completion times
                    parallelism for the job, container registry, assigned identity etc details.
  - Template: Job template containing job containers deatails, cpu, memory resource details.
  - Labels: Labels for the job. It has the managed environment name and workloadprofile name for the job.
  - Status: Status of the container app Job. It has jobRunningState and jobProvisioningState.
                   Possible values are for jobRunningState: Running, Suspended.
                   Possible values for jobProvisioningState: Provisioned, Failed.
")]
        public Task<string> GetJobDefinition(
            [Description("The name of the Container App Job")] string containerAppJobName,
            [Description("The Azure region")] string region,
            [Description("The resource group of the Container App Job")] string cappResourceGroup,
            [Description("The subscription ID of the Container App Job")] string cappSubscription,
            [Description("Name of the managed cluster")] string cappClusterName,
            [Description("The start of the time range for the query")] DateTime queryFrom,
            [Description("The end of the time range for the query")] DateTime queryTo)
        {
            return _plugin.GetJobDefinition(containerAppJobName, region, cappResourceGroup, cappSubscription, cappClusterName, queryFrom, queryTo);
        }

        [KernelFunction(KernelFunctionNames.Jobs.GetJobExecutionJson)]
        [Description(@"Get the job execution's final status for a Container App Job. It contains detailed status of the given
job execution, whether succeeded or failed, if failed, failure reason and message details in JobExecutionStatusDetails column.
Projects:
  - PreciseTimeStamp: Precise timestamp of the event.
  - JobExecutionName: Name of the job execution.
  - JobExecutionStatus: Status of the job execution, ex: Succeeded, Failed.
  - JobExecutionStatusDetails: Detailed status of the job execution, if failed, it has reason for failure, message etc useful details.
")]
        public Task<string> GetJobExecutionFinalStatus(
            [Description("The Azure region")] string region,
            [Description("Name of the managed cluster")] string managedClusterName,
            [Description("Name of the jobExecutionName")] string jobExecutionName,
            [Description("The start of the time range for the query")] DateTime queryFrom,
            [Description("The end of the time range for the query")] DateTime queryTo)
        {
            return _plugin.GetJobExecutionFinalStatus(region, managedClusterName, jobExecutionName, queryFrom, queryTo);
        }

        [KernelFunction(KernelFunctionNames.Jobs.GetJobExecutionEvents)]
        [Description(@"Get full lifecycle events for a specific Container App Job execution from EventProcessorEvents.
Projects:
  - PreciseTimeStamp: Precise timestamp of the event.
  - msg: Log message of the event.
  - Reason: Reason for the event.
  - Count: Count of the event.
  - Type: Type of the event, ex: Warning, Normal, Error etc.

")]
        public Task<string> GetJobExecutionEvents(
            [Description("The Azure region")] string region,
            [Description("The name of the job execution")] string jobExecutionName,
            [Description("Name of the managed cluster")] string managedClusterName,
            [Description("The start of the time range for the query (UTC datetime)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime)")] DateTime queryTo)
        {
            return _plugin.GetJobExecutionEvents(region, jobExecutionName, managedClusterName, queryFrom, queryTo);
        }

        [KernelFunction(KernelFunctionNames.Jobs.GetAllJobExecutionsErrorEvents)]
        [Description(@"Gets all error events for all job executions of a given ContainerApp Job.")]
        public Task<string> GetAllJobExecutionsErrorEvents(
            [Description("The Azure region")] string region,
            [Description("Name of the managed cluster")] string managedClusterName,
            [Description("Name of the container app job")] string containerAppJobName,
            [Description("The start of the time range for the query (UTC datetime)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime)")] DateTime queryTo)
        {
            return _plugin.GetAllJobExecutionsErrorEvents(region, managedClusterName, containerAppJobName, queryFrom, queryTo);
        }

        [KernelFunction(KernelFunctionNames.Jobs.GetAllJobExecutionsFinalStatus)]
        [Description(@"Gets the final status for all job executions of a given ContainerApp Job.")]
        public Task<string> GetAllJobExecutionsFinalStatus(
            [Description("The Azure region")] string region,
            [Description("Name of the managed cluster")] string managedClusterName,
            [Description("Name of the container app job")] string containerAppJobName,
            [Description("The start of the time range for the query (UTC datetime)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime)")] DateTime queryTo)
        {
            return _plugin.GetAllJobExecutionsFinalStatus(region, managedClusterName, containerAppJobName, queryFrom, queryTo);
        }

        [KernelFunction(KernelFunctionNames.Jobs.GetKedaEventsForJobScaledJobs), Description("Get KEDA events for scaled jobs.")]
        public Task<string> GetKedaEventsForJobScaledJobs(
            [Description("The Azure region")] string region,
            [Description("Name of the managed cluster")] string managedClusterName,
            [Description("The name of the Container App Job")] string containerAppJobName,
            [Description("The start of the time range for the query (UTC datetime)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime)")] DateTime queryTo)
        {
            return _plugin.GetKedaEventsForJobScaledJobs(region, managedClusterName, containerAppJobName, queryFrom, queryTo);
        }

        [KernelFunction(KernelFunctionNames.Jobs.GetLegionVKEventsForJobsRunningConsumptionV2), Description("Get Legion VK events for jobs running on consumption V2.")]
        public Task<string> GetLegionVKEventsForJobsRunningConsumptionV2(
            [Description("The Azure region")] string region,
            [Description("Name of the managed cluster")] string managedClusterName,
            [Description("The name of the job execution")] string jobExecutionName,
            [Description("The start of the time range for the query (UTC datetime)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime)")] DateTime queryTo)
        {
            return _plugin.GetLegionVKEventsForJobsRunningConsumptionV2(region, managedClusterName, jobExecutionName, queryFrom, queryTo);
        }
    }
}
