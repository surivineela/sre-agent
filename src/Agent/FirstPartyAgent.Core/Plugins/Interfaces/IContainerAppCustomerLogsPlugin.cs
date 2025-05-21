// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Plugins
{
    public interface IContainerAppCustomerLogsPlugin
    {
        Task<string> GetLogConfiguration(string region, DateTime fromDate, DateTime toDate, Guid customerSubscriptionId, string managedEnvironmentName, string managedClusterName);

        Task<string> GetEventProcessorErrors(string region, DateTime fromDate, DateTime toDate, string managedClusterName, string containerAppOrJobName);
    }
}
