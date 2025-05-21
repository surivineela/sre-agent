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

        [KernelFunction(KernelFunctionNames.Jobs.GetJobDefinition), Description("Get the job definition (spec) for a given Container App Job.")]
        public Task<string> GetJobDefinition(
            [Description("The name of the Container App Job")] string cappName,
            [Description("The Azure region where the Container App Job is located, e.g., westus3")] string region,
            [Description("The resource group of the Container App Job")] string cappResourceGroup,
            [Description("The subscription ID of the Container App Job")] string cappSubscription,
            [Description("Name of the managed cluster")] string cappClusterName,
            [Description("The start of the time range for the query (UTC datetime string)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime string)")] DateTime queryTo)
        {
            return _plugin.GetJobDefinition(cappName, region, cappResourceGroup, cappSubscription, cappClusterName, queryFrom, queryTo);
        }

        [KernelFunction(KernelFunctionNames.Jobs.GetJobExecutionJson)]
        [Description(@"Get the execution details for a Container App Job (v1 Job object). It contains detailed status of the
each job executions, whether succeeded or failed, if failed, failure reason and message details.")]
        public Task<string> GetJobExecutionJson(
            [Description("The Azure region where the Container App Job is located, e.g., westus3")] string region,
            [Description("Name of the managed cluster")] string cappClusterName,
            [Description("Name of the job")] string jobName,
            [Description("The start of the time range for the query (UTC datetime string)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime string)")] DateTime queryTo)
        {
            return _plugin.GetJobExecutionJson(region, jobName, cappClusterName, queryFrom, queryTo);
        }

        [KernelFunction(KernelFunctionNames.Jobs.GetEventsForJobExecution), Description("Get events for a specific Container App Job execution from EventProcessorEvents.")]
        public Task<string> GetEventsForJobExecution(
            [Description("The Azure region where the Container App Job is located, e.g., westus3")] string region,
            [Description("The name of the job execution")] string jobExecutionName,
            [Description("Name of the managed cluster")] string cappClusterName,
            [Description("The start of the time range for the query (UTC datetime string)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime string)")] DateTime queryTo)
        {
            return _plugin.GetEventsForJobExecution(region, jobExecutionName, cappClusterName, queryFrom, queryTo);
        }

        [KernelFunction(KernelFunctionNames.Jobs.GetJobExecutionEventsController), Description("Get controller events for a specific Container App Job execution.")]
        public Task<string> GetJobExecutionEventsController(
            [Description("The Azure region where the Container App Job is located, e.g., westus3")] string region,
            [Description("The name of the job execution")] string jobExecutionName,
            [Description("Name of the managed cluster")] string cappClusterName,
            [Description("The start of the time range for the query (UTC datetime string)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime string)")] DateTime queryTo)
        {
            return _plugin.GetJobExecutionEventsController(region, jobExecutionName, cappClusterName, queryFrom, queryTo);
        }

        [KernelFunction(KernelFunctionNames.Jobs.GetKedaEventsForJobScaledJobs), Description("Get KEDA events for scaled jobs.")]
        public Task<string> GetKedaEventsForJobScaledJobs(
            [Description("The Azure region where the Container App Job is located, e.g., westus3")] string region,
            [Description("The name of the Container App Job")] string cappName,
            [Description("Name of the managed cluster")] string cappClusterName,
            [Description("The start of the time range for the query (UTC datetime string)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime string)")] DateTime queryTo)
        {
            return _plugin.GetKedaEventsForJobScaledJobs(region, cappName, cappClusterName, queryFrom, queryTo);
        }

        [KernelFunction(KernelFunctionNames.Jobs.GetLegionVKEventsForJobsRunningConsumptionV2), Description("Get Legion VK events for jobs running on consumption V2.")]
        public Task<string> GetLegionVKEventsForJobsRunningConsumptionV2(
            [Description("The Azure region where the Container App Job is located, e.g., westus3")] string region,
            [Description("The name of the job execution")] string jobExecutionName,
            [Description("Name of the managed cluster")] string cappClusterName,
            [Description("The start of the time range for the query (UTC datetime string)")] DateTime queryFrom,
            [Description("The end of the time range for the query (UTC datetime string)")] DateTime queryTo)
        {
            return _plugin.GetLegionVKEventsForJobsRunningConsumptionV2(region, jobExecutionName, cappClusterName, queryFrom, queryTo);
        }
    }
}
