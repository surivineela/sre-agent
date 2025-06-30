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

        [Description(@"List all the nodes names for the given Managed Cluster.
This operation will return the names of all the nodes in the specified Managed Cluster within the given time range.
These node names can be used to query the node heartbeat and Swift Network Container heartbeat of each node.
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

        [Description(@"Get the heartbeat status of all the nodes in the specified Managed Cluster.
Tool outputs:
- StartTime: The start time of the heartbeat data.
- EndTime: The end time of the heartbeat data.
- NodeName: The name of the node.
- NodeHeartbeat: The heartbeat status of the node. It can be 'Ready' or 'Not Ready'.

Important Notes:
Use this tool to identify which nodes are operational ('Ready') and when. Nodes marked 'Ready' are expected to have active NetworkContainers. This tool is essential for detecting nodes that may lack corresponding container activity, indicating potential network issues.
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

        [Description(@"Get the Swift Network Container heartbeat status of all the nodes in the specified Managed Cluster.
Tool outputs:
- StartTime: The start time of the heartbeat data.
- EndTime: The end time of the heartbeat data.
- NodeName: The name of the node where the Swift Network Container is running.
- NetworkContainerID: The ID of the network container.
- NetworkContainerHeartbeat: The heartbeat status of the Swift Network Container. It is expected to be 'Alive'.

Important Notes:
Use this tool to verify that each 'Ready' node has a corresponding 'Alive' NetworkContainer. Missing or mismatched time windows between node and container heartbeats indicates network connectivity failures.
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

        [Description(@"Retrieves the Swift Network Container creation and deletion events for the specified Managed Cluster node.
Tool outputs:
- TimeStamp: The timestamp of the event.
- OperationName: The name of the operation, such as 'CreateSwiftNetworkContainer' or 'DeleteSwiftNetworkContainer'. It can also be empty.
- message: message describing the event.
- Response: response of the operation, including httpStatusCode, networkContainerId, etc.
- error: detailed error message if the operation failed.

Important Notes:
- It is expected that the Swift Network Container is deleted after the node is deleted.
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

        [Description(@"Identify and list NetworkContainerID that might be leaked.
This tool will list all NetworkContainerIDs that may be leaked (those network containers that were not deleted after their associated node was removed) in the specified Managed Cluster.

Important Notes:
This tool is not accurate and may return false positives. It is recommended to use the GetDeleteNetworkContainerOperation tool and GetAggregatedNetworkContainerHealthEvent tool to double-check the deletion status of each NetworkContainerID.
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

        [Description(@"Retrieves the delete operation details for a specific NetworkContainerID.
This tool will return all the DeleteNetworkContainer operations with detailed Message.
- If no results are returned, it means there is no delete operation for the specified NetworkContainerID within the given time range. You need to highlight it since no delete operation was found it may indicate that the NetworkContainerID is leaked.
- If the results are not empty, it means the delete operation was performed successfully or failed. The Message field will provide more details about the operation. Always show timestamp, NodeId, ContainerId, OperationName and Message fields in the result.
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

        [Description(@"Retrieves the aggregated health event for a specific NetworkContainerID.
The return results can be used to double-check whether the NetworkContainerID is leaked or not.
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

        [Description(@"This function queries the NetworkServiceManagerEvents table to identify Swift network container errors related to GRE key conflicts in environments using Internal Load Balancers (ILB).
This is particularly useful for diagnosing issues where internal traffic fails to route correctly due to overlapping GRE keys.
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
    }
}
