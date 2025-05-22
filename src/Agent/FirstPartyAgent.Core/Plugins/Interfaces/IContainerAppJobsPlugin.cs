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

        Task<string> GetJobExecutionFinalStatus(
            string region,
            string managedClusterName,
            string jobExecutionName,
            DateTime queryFrom,
            DateTime queryTo);

        Task<string> GetJobExecutionEvents(
            string region,
            string jobExecutionName,
            string managedClusterName,
            DateTime queryFrom,
            DateTime queryTo);

        Task<string> GetAllJobExecutionsErrorEvents(
            string region,
            string managedClusterName,
            string containerAppJobName,
            DateTime queryFrom,
            DateTime queryTo);

        Task<string> GetAllJobExecutionsFinalStatus(
            string region,
            string managedClusterName,
            string containerAppJobName,
            DateTime queryFrom,
            DateTime queryTo);

        Task<string> GetKedaEventsForJobScaledJobs(
            string region,
            string managedClusterName,
            string containerAppJobName,
            DateTime queryFrom,
            DateTime queryTo);

        Task<string> GetLegionVKEventsForJobsRunningConsumptionV2(
            string region,
            string managedClusterName,
            string jobExecutionName,
            DateTime queryFrom,
            DateTime queryTo);
    }
}
