// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Threading.Tasks;
using System.Collections.Generic;

namespace FirstPartyAgent.Plugins.Interfaces // Assuming a common interfaces namespace
{
    public interface IContainerAppJobsPlugin
    {
        Task<string> GetJobDefinition(
            string cappName,
            string region,
            string cappResourceGroup,
            string cappSubscription,
            string cappClusterName,
            DateTime queryFrom,
            DateTime queryTo);

        Task<string> GetJobExecutionJson(
            string region,
            string jobName,
            string cappClusterName,
            DateTime queryFrom,
            DateTime queryTo);

        Task<string> GetEventsForJobExecution(
            string region,
            string jobExecutionName,
            string cappClusterName,
            DateTime queryFrom,
            DateTime queryTo);

        Task<string> GetJobExecutionEventsController(
            string region,
            string jobExecutionName,
            string cappClusterName,
            DateTime queryFrom,
            DateTime queryTo);

        Task<string> GetKedaEventsForJobScaledJobs(
            string region,
            string cappName,
            string cappClusterName,
            DateTime queryFrom,
            DateTime queryTo);

        Task<string> GetLegionVKEventsForJobsRunningConsumptionV2(
            string region,
            string jobExecutionName,
            string cappClusterName,
            DateTime queryFrom,
            DateTime queryTo);
    }
}
