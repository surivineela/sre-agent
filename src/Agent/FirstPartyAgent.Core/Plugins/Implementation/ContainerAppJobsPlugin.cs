// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using FirstPartyAgent.Plugins;
using FirstPartyAgent.Plugins.Interfaces;

namespace FirstPartyAgent.Core.Plugins.Implementation
{
    public class ContainerAppJobsPlugin : IContainerAppJobsPlugin
    {
        private readonly IKustoPluginChat _kustoPluginChat;

        public ContainerAppJobsPlugin(IKustoPluginChat kustoPluginChat)
        {
            _kustoPluginChat = kustoPluginChat;
        }

        public Task<string> GetJobDefinition(
            string cappName,
            string region,
            string cappResourceGroup,
            string cappSubscription,
            string cappClusterName,
            DateTime queryFrom,
            DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "cappName", cappName },
                { "cappResourceGroup", cappResourceGroup },
                { "cappSubscription", cappSubscription },
                { "cappClusterName", cappClusterName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetJobDefinition", region, args);
        }

        public Task<string> GetJobExecutionFinalStatus(
            string region,
            string managedClusterName,
            string jobExecutionName,
            DateTime queryFrom,
            DateTime queryTo)
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

        public Task<string> GetJobExecutionEvents(
            string region,
            string jobExecutionName,
            string managedClusterName,
            DateTime queryFrom,
            DateTime queryTo)
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

        public Task<string> GetAllJobExecutionsErrorEvents(
            string region,
            string managedClusterName,
            string containerAppJobName,
            DateTime queryFrom,
            DateTime queryTo)
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

        public Task<string> GetAllJobExecutionsFinalStatus(
            string region,
            string managedClusterName,
            string containerAppJobName,
            DateTime queryFrom,
            DateTime queryTo)
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

        public Task<string> GetKedaEventsForJobScaledJobs(
            string region,
            string managedClusterName,
            string containerAppJobName,
            DateTime queryFrom,
            DateTime queryTo)
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

        public Task<string> GetLegionVKEventsForJobsRunningConsumptionV2(
            string region,
            string managedClusterName,
            string jobExecutionName,
            DateTime queryFrom,
            DateTime queryTo)
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
