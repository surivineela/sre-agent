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
            return _kustoPluginChat.ExecuteLocalFunctionAsync("JobJson", region, args);
        }

        public Task<string> GetJobExecutionJson(
            string region,
            string cappClusterName,
            string jobName,
            DateTime queryFrom,
            DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "cappClusterName", cappClusterName },
                { "jobName", jobName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("JobExecutionJson", region, args);
        }

        public Task<string> GetEventsForJobExecution(
            string region,
            string jobExecutionName,
            string cappClusterName,
            DateTime queryFrom,
            DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "jobExecutionName", jobExecutionName },
                { "cappClusterName", cappClusterName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("EventForAJobExecution", region, args);
        }

        public Task<string> GetJobExecutionEventsController(
            string region,
            string jobExecutionName,
            string cappClusterName,
            DateTime queryFrom,
            DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "jobExecutionName", jobExecutionName },
                { "cappClusterName", cappClusterName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("JobExecutionEventsController", region, args);
        }

        public Task<string> GetKedaEventsForJobScaledJobs(
            string region,
            string cappName,
            string cappClusterName,
            DateTime queryFrom,
            DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "cappName", cappName },
                { "cappClusterName", cappClusterName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("KedaEventsJobScaledJobs", region, args);
        }

        public Task<string> GetLegionVKEventsForJobsRunningConsumptionV2(
            string region,
            string jobExecutionName,
            string cappClusterName,
            DateTime queryFrom,
            DateTime queryTo)
        {
            var args = new Dictionary<string, string>
            {
                { "jobExecutionName", jobExecutionName },
                { "cappClusterName", cappClusterName },
                { "queryFrom", queryFrom.ToString() },
                { "queryTo", queryTo.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionOnClusterAsync("LegionVKEventsForJobsRunningConsumptionV2", "legioneus.eastus", "legion", args);
        }
    }
}
