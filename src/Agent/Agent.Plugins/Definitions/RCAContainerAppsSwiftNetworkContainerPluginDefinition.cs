using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class RCAContainerAppsSwiftNetworkContainerPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public RCAContainerAppsSwiftNetworkContainerPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description(@"""
        List all the nodes names for the given Managed Cluster.
        This operation will return the names of all the nodes in the specified Managed Cluster within the given time range.
        This tool can be helpful when you need to query the node heartbeat and Swift Network Container heartbeat of each node.

        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.

        Tool outputs:
        - NodeName: The name of the node.
        - StartTime: The start time of the node's status.
        - EndTime: The end time of of the node's activity.
        ")]
        public async Task<string> ListManagedClusterNodes(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("ListClusterNodes", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }

        [Description(@"""
        Get the heartbeat status of all the nodes in the specified Managed Cluster.
        This tool can help to identify which nodes are operational ('Ready') and when. Nodes marked 'Ready' are expected to have active NetworkContainers.
        This tool is essential for detecting nodes that may lack corresponding container activity, indicating potential network issues.

        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.

        Tool outputs:
        - StartTime: The start time of the heartbeat data.
        - EndTime: The end time of the heartbeat data.
        - NodeName: The name of the node.
        - NodeHeartbeat: The heartbeat status of the node. It can be 'Ready' or 'Not Ready'.
        ")]
        public async Task<string> GetManagedClusterNodesHeartbeat(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName
            )
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetClusterNodesHeartbeat", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }

        [Description(@"""
        Get the Swift Network Container heartbeat status of all the nodes in the specified Managed Cluster.
        This tool is helpful to verify that each 'Ready' node has a corresponding 'Alive' NetworkContainer. Missing or mismatched time windows between node and container heartbeats indicates network connectivity failures.

        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.

        Tool outputs:
        - StartTime: The start time of the heartbeat data.
        - EndTime: The end time of the heartbeat data.
        - NodeName: The name of the node runing the Swift Network Container.
        - NetworkContainerID: The ID of the network container.
        - NetworkContainerHeartbeat: The heartbeat status of the Swift Network Container. It is expected to be 'Alive'.
        ")]
        public async Task<string> GetManagedClusterNodesSwiftNetworkContainersHeartbeat(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetClusterNodesSwiftNetworkContainersHeartbeat", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }

        [Description(@"""
        Retrieves the Swift Network Container creation and deletion events for the specified Managed Cluster node.
        It is expected that the Swift Network Container is deleted after the node is deleted.

        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.

        Tool outputs:
        - TimeStamp: The timestamp of the event.
        - OperationName: The name of the operation, such as 'CreateSwiftNetworkContainer' or 'DeleteSwiftNetworkContainer'. It can also be empty.
        - message: message describing the event.
        - Response: response of the operation, including httpStatusCode, networkContainerId, etc.
        - error: detailed error message if the operation failed.
        ")]
        public async Task<string> GetSwiftNetworkContainerCreateAndDeleteEventsLog(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName,
            [Description("Name of the node retrieved in the ListManagedClusterNodes tool")] string nodeName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetSwiftNetworkContainerCreateDeleteEvents", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName },
                    { "nodeName", nodeName }
                });
        }

        [Description(@"""
        Identify and list NetworkContainerID that might be leaked.
        This tool will list all NetworkContainerIDs that may be leaked (those network containers that were not deleted after their associated node was removed) in the specified Managed Cluster.

        This tool is not accurate and may return false positives. It is recommended to use the GetDeleteNetworkContainerOperation tool and GetAggregatedNetworkContainerHealthEvent tool to double-check the deletion status of each NetworkContainerID.

        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.

        Tool outputs:
        - NodeName: The name of the node where the NetworkContainerID was created.
        - NetworkContainerID: The ID of the network container that may be leaked.
        ")]
        public async Task<string> ListPotentialLeakedNetworkContainer(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("ListPotentialLeakedNetworkContainer", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }

        [Description(@"""
        Retrieves the delete operation details for a specific NetworkContainerID.
        This tool will return all the DeleteNetworkContainer operations with detailed Message.
        - If no results are returned, it means there is no delete operation for the specified NetworkContainerID within the given time range. It's important since no delete operation was found it may indicate that the NetworkContainerID is leaked.
        - If the results are not empty, it means the delete operation was performed successfully or failed. The Message field will provide more details about the operation.

        Tool outputs:
        - TimeStamp: The timestamp of the delete operation.
        - NodeId: The ID of the node where the delete operation was performed.
        - ContainerId: The ID of the network container that was deleted.
        - OperationName: The name of the operation.
        - Message: A detailed message about the delete operation.
        ")]
        public async Task<string> GetDeleteNetworkContainerOperation(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("NetworkContainerID retrieved in the ListPotentialLeakedNetworkContainer tool")] string networkContainerID)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetDeleteNetworkContainerOperation", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "networkContainerID", networkContainerID }
                });
        }

        [Description(@"""
        Retrieves the aggregated health event for a specific NetworkContainerID.
        The return results can be used to double-check whether the NetworkContainerID is leaked or not.

        Tool Outputs:
        - StartTime: The start time of the health event.
        - EndTime: The end time of the health event.
        - NetworkContainerId: The ID of the network container.
        - OwnDsMappingsStatus: If the field value is 0, it indicates that the NetworkContainerID is leaked.
        - CustomerAddress: If there are multiple customer addresses, it indicates that the NetworkContainerID is leaked.
        - HealthState: It shows the detailed message of the health event. It's usually empty if the NetworkContainerID is not leaked.
        - NodeId and ContainerId: these two fields are very important for the user to do further investigation. 
        ")]
        public async Task<string> GetAggregatedNetworkContainerHealthEvent(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("NetworkContainerID retrieved in the ListPotentialLeakedNetworkContainer tool")] string networkContainerID)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetAggregatedNetworkContainerHealthEvent", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "networkContainerID", networkContainerID }
                });
        }

        [Description(@"""
        This function queries the NetworkServiceManagerEvents table to identify Swift network container errors related to GRE key conflicts in environments using Internal Load Balancers (ILB).
        This is particularly useful for diagnosing issues where internal traffic fails to route correctly due to overlapping GRE keys.

        Tool Outputs:
        - TIMESTAMP: The timestamp of the event.
        - TaskName: The name of the task that generated the event.
        - Message: A detailed message describing the event. 
        ")]
        public async Task<string> TrackSwiftILBGreKeyConflicts(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("TrackSwiftILBGreKeyConflicts", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }

        [Description(@"""
        Get ASI page URL for the Load Balancer of a Managed Cluster.
        Tool Outputs:
        - The string of ASI page URL")]
        public Task<string> GetASIPageForManagedClusterLoadBalancer(
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Load Balancer Resource Url.")] string loadBalancerResourceUrl)
        {
            var basePath = "https://asi.azure.ms/services/ACE%20Network%20Tools/pages/Load%20Balancer";

            var args = $"ResourceUri={loadBalancerResourceUrl}" +
               $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("M/d/yyyy hh:mm:ss tt"))}" +
               $"&globalTo={Uri.EscapeDataString(toDate.ToString("M/d/yyyy hh:mm:ss tt"))}";

            var asiPageUri = $"{basePath}?{args}";

            return Task.FromResult($"ASI Page for managed cluster loader balancer: {asiPageUri}");
        }

        [Description(@"""
        Get the managed cluster's load balancer VipAvailability_DataPathAvailability and DipAvailability_HealthProbeStatus page URLs
        Tool Outputs:
        - VipAvailability_DataPathAvailability: URL string of VipAvailability_DataPathAvailability
        - DipAvailability_HealthProbeStatus: URL string of DipAvailability_HealthProbeStatus")]
        public async Task<string> GetVipAndDipAvailabilityUrls(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            string loadBalancerVipDipInfo;
            try
            {
                loadBalancerVipDipInfo = await _kustoPlugin.ExecuteLocalFunctionAsync("GetLoadBalancerVipDipInfo", region,
                    new Dictionary<string, string>
                    {
                        { "fromDate", fromDate.ToString() },
                        { "toDate", toDate.ToString() },
                        { "managedClusterName", managedClusterName }
                    });
            }
            catch (Exception ex)
            {
                return $"Error retrieving Load Balancer Vip and Dip information: {ex.Message}";
            }

            if (string.IsNullOrEmpty(loadBalancerVipDipInfo))
            {
                return "No Load Balancer Vip and Dip information found.";
            }
            else if (!loadBalancerVipDipInfo.Contains('\t'))
            {
                return "Invalid Load Balancer Vip and Dip information format.";
            }
            var parts = loadBalancerVipDipInfo.Split('\t');
            if (parts.Length < 4)
            {
                return "Invalid Load Balancer Vip and Dip information format.";
            }

            string mdmAccountName = parts[0];
            string nrpLbId = parts[1];
            string vip = parts[2];
            string altAddress = parts[3];

            long fromDateUnix = new DateTimeOffset(fromDate.ToUniversalTime()).ToUnixTimeMilliseconds();
            long toDateUnix = new DateTimeOffset(toDate.ToUniversalTime()).ToUnixTimeMilliseconds();


            string vipUrl = $"https://portal.microsoftgeneva.com/dashboard/slbv2prod/AzureMonitor/VipAvailability_DataPathAvailability?overrides=[{{\"query\":\"//dataSources\",\"key\":\"account\",\"replacement\":\"{mdmAccountName}\"}},{{\"query\":\"//*[id='Slbv2MDMAccount']\",\"key\":\"value\",\"replacement\":\"{mdmAccountName}\"}},{{\"query\":\"//*[id='VipPort']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='LoadBalancerArmId']\",\"key\":\"value\",\"replacement\":\"{nrpLbId}\"}},{{\"query\":\"//*[id='PublicIpArmId']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='VnetId']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='VipOrIlbPA']\",\"key\":\"value\",\"replacement\":\"{vip}\"}},{{\"query\":\"//*[id='Ring']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='AddressFamily']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='SubscriptionId']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='VipAddress']\",\"key\":\"value\",\"replacement\":\"{altAddress}\"}}]" +
               $"&globalStartTime={fromDateUnix}" +
               $"&globalEndTime={toDateUnix}" + "&pinGlobalTimeRange=true";

            string dipUrl = $"https://portal.microsoftgeneva.com/dashboard/slbv2prod/AzureMonitor/DipAvailability_HealthProbeStatus?overrides=[{{\"query\":\"//dataSources\",\"key\":\"account\",\"replacement\":\"{mdmAccountName}\"}},{{\"query\":\"//*[id='Slbv2MDMAccount']\",\"key\":\"value\",\"replacement\":\"{mdmAccountName}\"}},{{\"query\":\"//*[id='VipPort']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='LoadBalancerArmId']\",\"key\":\"value\",\"replacement\":\"{nrpLbId}\"}},{{\"query\":\"//*[id='PublicIpArmId']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='VnetId']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='VipOrIlbPA']\",\"key\":\"value\",\"replacement\":\"{vip}\"}},{{\"query\":\"//*[id='Ring']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='AddressFamily']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='SubscriptionId']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='VipAddress']\",\"key\":\"value\",\"replacement\":\"{altAddress}\"}},{{\"query\":\"//*[id='CaAddress']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='DipPort']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='ProtocolType']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='DipAddress']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='HostAddress']\",\"key\":\"value\",\"replacement\":\"\"}},{{\"query\":\"//*[id='VipInternalAddress']\",\"key\":\"value\",\"replacement\":\"{vip}\"}}]" +
               $"&globalStartTime={fromDateUnix}" +
               $"&globalEndTime={toDateUnix}" + "&pinGlobalTimeRange=true";

            return $"VipAvailability_DataPathAvailability: {vipUrl}, DipAvailability_HealthProbeStatus: {dipUrl}";
        }
    }
}
