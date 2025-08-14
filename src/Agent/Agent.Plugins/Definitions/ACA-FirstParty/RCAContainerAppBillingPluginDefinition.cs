using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;
using Agent.Plugins.Kusto;
using Agent.Plugins.KustoPlugin;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppBillingPluginDefinition
    {
        private readonly IKustoPlugin _kustoPluginChat;

        public RCAContainerAppBillingPluginDefinition(IKustoPlugin kustoPluginChat)
        {
            _kustoPluginChat = kustoPluginChat;
        }

        [Description(@"""
        Purpose:
        Retrieves billing meter usage for the specific azure resource like Container App, Job, or Managed Environment within a specified time range.

        Scenario:
        Use this tool to analyze the costs incurred by a customer for a specific Container App. This tool helps validate customer reports of high charges and identify billing patterns over time.

        Output:
        Returns tab-separated table data in CSV format. Columns:
        - PreciseTimeStamp: Timestamp of aggregated billing meter usage
        - BillingMeterLabel: Billing meter name, e.g., `Idle vCPU`, `Active Memory`, etc.
        - Usage: Total usage
        """
        )]
        public Task<string> GetBillingMeterUsages(
            [Description("Azure region.")] string region,
            [Description("ARM ID of the azure resource to retrieve billing details like containerapp or job or managed environment")] string resourceId,
            [Description("Start of the time range for the query.")] DateTime fromDate,
            [Description("End of the time range for the query.")] DateTime toDate)
        {
            var args = new Dictionary<string, string>
            {
                { "resourceId", resourceId },
                { "region", region },
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetBillingMeterUsages", region, args);
        }


        [Description(@"""
        Purpose:
        Retrieves the CPU usage for a specified container app resource within a given time range.

        Scenario:
        Use this tool to analyze container app CPU consumption patterns and identify spikes, drops, or unusual usage that may correlate with billing meter changes.

        Output:
        Returns tab-separated table data in CSV format. Columns:
        - Timestamp: Timestamp (UTC)
        - CpuUsageNanoCores: CPU usage in NanoCores
        """
        )]
        public Task<string> GetContainerAppCpuUsage(
            [Description("Azure region.")] string region,
            [Description("Resource ARM ID of the Container App.")] string resourceId,
            [Description("Start of the time range for the query.")] DateTime fromDate,
            [Description("End of the time range for the query.")] DateTime toDate)
        {
            var args = new Dictionary<string, string>
            {
                { "resourceId", resourceId },
                { "region", region },
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetContainerAppCpuUsage", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves the memory usage for a specified container app resource within a given time range.

        Scenario:
        Use this tool to analyze container app memory consumption patterns and identify spikes, drops, or unusual usage that may correlate with billing meter changes.

        Output:
        Returns tab-separated table data in CSV format. Columns:
        - Timestamp: Timestamp (UTC)
        - MemoryUsageInBytes: Memory usage in bytes
        """
        )]
        public Task<string> GetContainerAppMemoryUsage(
            [Description("Azure region.")] string region,
            [Description("Resource ARM ID of the Container App.")] string resourceId,
            [Description("Start of the time range for the query.")] DateTime fromDate,
            [Description("End of the time range for the query.")] DateTime toDate)
        {
            var args = new Dictionary<string, string>
            {
                { "resourceId", resourceId },
                { "region", region },
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetContainerAppMemoryUsage", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves the replica count per revision for a specified container app within the given time range.

        Scenario:
        Use this tool when the billing meter usage of container app changed during the investigation period, to investigate if it is caused due to changes in the number of replicas.

        Output:
        Returns tab-separated table data in CSV format. Columns:
        - StartTime: Start of the time window
        - EndTime: End of the time window
        - RevisionName: Name of the revision
        - ReplicaCount: Number of replicas of the revision during the time window
        """
        )]
        public Task<string> GetContainerAppReplicaCount(
            [Description("Azure region.")] string region,
            [Description("Resource ARM ID of the Container App.")] string resourceId,
            [Description("Start of the time range for the query.")] DateTime fromDate,
            [Description("End of the time range for the query.")] DateTime toDate)
        {
            var args = new Dictionary<string, string>
            {
                { "resourceId", resourceId },
                { "region", region },
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetContainerAppReplicaCount", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves minReplicaCount changes for a specified Container App in a time range.

        Scenario:
        Use this tool when the there is a change in the active or idle billing meter usage of container app to investigate if it is caused due to changes in the minReplicaCount.

        Output:
        Returns tab-separated table data in CSV format. Columns:
        - StartTime: Start of the time window
        - EndTime: End of the time window
        - MinReplicaCount: minReplicaCount of container app during the time window
        """
        )]
        public Task<string> GetMinReplicaCountChanges(
            [Description("Azure region.")] string region,
            [Description("Container App name.")] string appName,
            [Description("Resource group name.")] string resourceGroup,
            [Description("Subscription ID.")] string subscriptionId,
            [Description("Managed cluster name.")] string clusterName,
            [Description("Start of the time range for the query.")] DateTime fromDate,
            [Description("End of the time range for the query.")] DateTime toDate)
        {
            var args = new Dictionary<string, string>
            {
                { "appName", appName },
                { "resourceGroup", resourceGroup },
                { "subscriptionId", subscriptionId },
                { "clusterName", clusterName },
                { "region", region },
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetMinReplicaCountChanges", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves the CPU usage for a specified job resource within a given time range.

        Scenario:
        Use this tool to analyze job CPU consumption patterns and identify spikes, drops, or unusual usage that may correlate with billing meter changes.

        Output:
        Returns tab-separated table data in CSV format. Columns:
        - Timestamp: Timestamp (UTC)
        - CpuUsageNanoCores: CPU usage in NanoCores
        """
        )]
        public Task<string> GetJobCpuUsage(
            [Description("Azure region.")] string region,
            [Description("Resource ARM ID of the Job.")] string resourceId,
            [Description("Start of the time range for the query.")] DateTime fromDate,
            [Description("End of the time range for the query.")] DateTime toDate)
        {
            var args = new Dictionary<string, string>
            {
                { "resourceId", resourceId },
                { "region", region },
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetJobCpuUsage", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves the memory usage for a specified job resource within a given time range.

        Scenario:
        Use this tool to analyze job memory consumption patterns and identify spikes, drops, or unusual usage that may correlate with billing meter changes.

        Output:
        Returns tab-separated table data in CSV format. Columns:
        - Timestamp: Timestamp (UTC)
        - MemoryUsageInBytes: Memory usage in bytes
        """
        )]
        public Task<string> GetJobMemoryUsage(
            [Description("Azure region.")] string region,
            [Description("Resource ARM ID of the Job.")] string resourceId,
            [Description("Start of the time range for the query.")] DateTime fromDate,
            [Description("End of the time range for the query.")] DateTime toDate)
        {
            var args = new Dictionary<string, string>
            {
                { "resourceId", resourceId },
                { "region", region },
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() }
            };
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetJobMemoryUsage", region, args);
        }

        [Description(@"""
        Purpose:
        Retrieves billing pod health and container failures for the specified managed cluster and time range.

        Scenario:
        Use this tool to assess health and restarts of billing pods, and identify degraded pods or container failures impacting metering.

        Output:
        Returns tab-separated table data in CSV format. Columns:
        - StartTime: Start time of the pod/container health window
        - EndTime: End time of the pod/container health window
        - PodName: Name of the pod
        - NodeName: Name of the node
        - PodStatus: Status of the pod
        - Health: Health status (Healthy/Degraded)
        - restartCount: Number of restarts
        - ContainerName: Name of the container (if applicable)
        - ContainerState: State of the container (Ready/Not Ready, if applicable)
        - ContainerImage: Image of the container (if applicable)
        """
        )]
        public Task<string> GetBillingPodHealth(
            [Description("Azure region.")] string region,
            [Description("Managed cluster name.")] string managedClusterName,
            [Description("Start of the time range for the query.")] DateTime fromDate,
            [Description("End of the time range for the query.")] DateTime toDate)
        {
            return _kustoPluginChat.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "podNamePrefix", "k8se-billing" },
                    { "podNamespace", "k8se-system" }
                });
        }
    }
}
