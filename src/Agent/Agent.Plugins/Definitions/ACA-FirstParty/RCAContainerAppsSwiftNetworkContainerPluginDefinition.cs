using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin]
    public class RCAContainerAppsSwiftNetworkContainerPluginDefinition
    {
        private readonly IKustoPlugin _kustoPlugin;

        public RCAContainerAppsSwiftNetworkContainerPluginDefinition(IKustoPlugin kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description("""
Purpose:
List all the node names for the given Managed Cluster.

Scenario:
Use this tool when you need to query the node heartbeat and Swift Network Container heartbeat of each node within a specified time range.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- NodeName: The name of the node
- StartTime: The start time of the node's status
- EndTime: The end time of the node's activity
""")]
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

        [Description("""
Purpose:
Get the heartbeat status of all the nodes in the specified Managed Cluster.

Scenario:
Use this tool to identify which nodes are operational ('Ready') and when. Essential for detecting nodes that may lack corresponding container activity, indicating potential network issues.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- StartTime: The start time of the heartbeat data
- EndTime: The end time of the heartbeat data
- NodeName: The name of the node
- NodeHeartbeat: The heartbeat status of the node (Ready or Not Ready)
""")]
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

        [Description("""
Purpose:
Get the Swift Network Container heartbeat status of all the nodes in the specified Managed Cluster.

Scenario:
Use this tool to verify that each 'Ready' node has a corresponding 'Alive' NetworkContainer. Missing or mismatched time windows between node and container heartbeats indicates network connectivity failures.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- StartTime: The start time of the heartbeat data
- EndTime: The end time of the heartbeat data
- NodeName: The name of the node running the Swift Network Container
- NetworkContainerID: The ID of the network container
- NetworkContainerHeartbeat: The heartbeat status of the Swift Network Container (expected to be 'Alive')
""")]
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

        [Description("""
Purpose:
Retrieves the Swift Network Container creation and deletion events for the specified Managed Cluster node.

Scenario:
Use this tool to verify that the Swift Network Container is properly deleted after the node is deleted and to investigate container lifecycle events.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- TimeStamp: The timestamp of the event
- OperationName: The name of the operation (CreateSwiftNetworkContainer, DeleteSwiftNetworkContainer, or empty)
- message: Message describing the event
- Response: Response of the operation, including httpStatusCode, networkContainerId, etc.
- error: Detailed error message if the operation failed
""")]
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

        [Description("""
Purpose:
Identify and list NetworkContainerID that might be leaked.

Scenario:
Use this tool to find network containers that may be leaked (not deleted after their associated node was removed) in the specified Managed Cluster. Note: This tool may return false positives - use GetDeleteNetworkContainerOperation and GetAggregatedNetworkContainerHealthEvent tools to verify.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- NodeName: The name of the node where the NetworkContainerID was created
- NetworkContainerID: The ID of the network container that may be leaked
""")]
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

        [Description("""
Purpose:
Retrieves the delete operation details for a specific NetworkContainerID.

Scenario:
Use this tool to verify deletion operations for a NetworkContainerID. If no results are returned, the NetworkContainerID may be leaked. If results exist, check the Message field for operation success or failure details.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- TimeStamp: The timestamp of the delete operation
- NodeId: The ID of the node where the delete operation was performed
- ContainerId: The ID of the network container that was deleted
- OperationName: The name of the operation
- Message: A detailed message about the delete operation
""")]
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

        [Description("""
Purpose:
Retrieves the aggregated health event for a specific NetworkContainerID.

Scenario:
Use this tool to double-check whether a NetworkContainerID is leaked or not by examining health events and status indicators.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- StartTime: The start time of the health event
- EndTime: The end time of the health event
- NetworkContainerID: The ID of the network container
- OwnDsMappingsStatus: If value is 0, indicates the NetworkContainerID is leaked
- NodeIP: Multiple NodeIPs indicate the NetworkContainerID is leaked
- NodeId and ContainerId: Important fields for further investigation
- HealthStatus: Detailed message of the health event (usually empty if not leaked)
""")]
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

        [Description("""
Purpose:
Queries the NetworkServiceManagerEvents table to identify Swift network container errors related to GRE key conflicts in environments using Internal Load Balancers (ILB).

Scenario:
Use this tool to diagnose issues where internal traffic fails to route correctly due to overlapping GRE keys in ILB environments.

Output:
Returns table data in CSV format with TAB separators. Column headers:
- TIMESTAMP: The timestamp of the event
- TaskName: The name of the task that generated the event
- Message: A detailed message describing the event
""")]
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

        [Description("""
Purpose:
Get ASI page URL for the Load Balancer of a Managed Cluster.

Scenario:
Use this tool to generate an ASI dashboard URL for analyzing load balancer performance and metrics for a managed cluster.

Output:
Returns the ASI page URL string for the managed cluster load balancer.
""")]
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

        [Description("""
Purpose:
Get the managed cluster's load balancer VipAvailability_DataPathAvailability and DipAvailability_HealthProbeStatus page URLs.

Scenario:
Use this tool to generate dashboard URLs for monitoring VIP data path availability and DIP health probe status for load balancer troubleshooting.

Output:
Returns formatted URL strings:
- VipAvailability_DataPathAvailability: URL for VIP data path availability dashboard
- DipAvailability_HealthProbeStatus: URL for DIP health probe status dashboard
""")]
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
