using System.ComponentModel;
using Agent.Framework;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppsIngressPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;

        public RCAContainerAppsIngressPluginDefinition(IKustoPluginChat kustoPlugin)
        {
            _kustoPlugin = kustoPlugin;
        }

        [Description("""
        Retrieves managed cluster details for a specified Container App.
        Use this tool when you need to identify the underlying managed cluster of a Container App to troubleshoot connectivity or configuration issues.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
        Tool outputs:
        - managedClusterName: Managed Cluster Name of the given Container App.
        - environmentType: Environment type of the Container App.Can be either 'V1' or 'V2'.
        - hasCustomerVnetForEnv: Indicates whether the Container App has a customer VNet configured for the environment.
        """
        )]
        public Task<string> GetContainerAppManagedCluster(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppManagedCluster", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "subscriptionId", subscriptionId },
                { "resourceGroupName", resourceGroupName },
                { "containerAppName", containerAppName }
            });
        }

        [Description("""
        Retrieves Envoy pod logs from a specified Managed Cluster of container app.
        Use this tool when diagnosing Envoy pod issues, such as connectivity failures, routing problems, or unexpected pod behavior.

        Output: Returns tab-separated table data in CSV format. The first line contains these column headers:
            - TimeStamp: UTC Time of the log event.
            - NodeName: Name of the node running the Envoy pod.
            - PodName: Name of the Envoy pod.
            - Log: Content of the Envoy pod log entry.
        """
        )]
        public Task<string> GetEnvoyPodLogs(
            [Description("Azure region of the container app.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Managed Cluster name of the container app.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvoyPodLogs", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
            });
        }

        [Description("""
        Retrieves Container Apps Envoy Controller Logs to troubleshoot ingress and network routing issues.
                    
        Use this tool when investigating Envoy controller events, such as envoy resources reconciler errors.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
        Tool outputs:
        - PreciseTimeStamp: Envoy controller log timestamp.
        - LogMessage: Envoy controller events log message.
        - error: Envoy controller events error message.
        - NodeName: Name of the node running the Envoy pod.
        - PodId: Id of the Envoy pod.
        - PodName: Name of the Envoy pod.
        - ContainerName: Pod container name. There can be multiple containers in a pod.
        - ContainerImage: The docker image used by the pod container.
        - caller: The source component or service that initiated the controller event or operation.
        """
        )]
        public Task<string> GetEnvoyControllerLogs(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Managed Cluster Name of the container app.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvoyControllerLogs", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
            });
        }

        [Description("""
        Retrieves time series data of Container Apps Envoy access requests grouped by HTTP status codes at the Container App level.
        Use this tool to get an overview of the requests received by Envoy in a Container App, categorized by HTTP status codes.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
        Tool outputs:
        - TimeStamp: Timestamp of the envoy access request.
        - Count: Count of envoy access requests.
        - seriesName: Name of the envoy access request series (e.g., Http 2xx Count, Http 3xx Count, Http 4xx Count, Http 5xx Count).
        """
        )]
        public Task<string> GetEnvoyAccessRequestCountTimeSeries(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Managed Cluster Name of the container app.")] string managedClusterName,
            [Description("The container app name.")] string containerAppName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvoyAccessRequestCountTimeSeries", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "containerAppName", containerAppName }
            });
        }

        [Description("""
        Retrieves time series data of Container Apps Envoy access requests at the managed cluster level.
        Use this tool when analyzing overall traffic patterns, monitoring request volumes across the entire managed cluster.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
        Tool outputs:
        - TimeStamp: Timestamp of the envoy access request.
        - Count: Count of envoy access requests.
        """
        )]
        public Task<string> GetManagedClusterLevelEnvoyAccessRequestCount(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Managed Cluster Name of the container app.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetManagedClusterLevelEnvoyAccessRequestCount", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
            });
        }

        [Description("""
        Retrieves detailed Container Apps Envoy Access Logs.
        Use this tool when analyzing detailed Container Apps Envoy access logs, such as identifying specific request patterns, troubleshooting issues with specific requests.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
        Tool outputs:
        - FirstSeen: Start time of the current kind of envoy access log.
        - LastSeen: End time of the current kind of envoy access log.
        - max_RequestDuration: Maximum request duration of this kind of envoy access log.
        - Count: Count of this kind of envoy access log.
        - Authority: Request access domain name.
        - Method: HTTP request methods.
        - Path: Request access path.
        - Protocol: Internet protocol.
        - Status: HTTP response status (e.g., 200, 503).
        - ResponseCodeDetails: Response code details (e.g., via_upstream, downstream_remote_disconnect).
        - UpstreamHost: The upstream host's IP address and port in the format <ip-address>:<port> (e.g., 100.100.202.85:8080).
        """)]
        public Task<string> GetEnvoyAccessLogs(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Managed Cluster Name of the container app.")] string managedClusterName,
            [Description("The container app name.")] string containerAppName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetEnvoyAccessLogs", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "containerAppName", containerAppName  }
            });
        }

        [Description("""
        Retrieves Swift Networking Events from the Container Apps managed environment.
        Use this tool when troubleshooting Swift networking connectivity issues, analyzing configuration problems, or swift networking events failures.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.

        Tool outputs:
        - PreciseTimeStamp: Swift networking event timestamp.
        - logger: Swift networking event logger.
        - LogMessage: Swift networking event log message.
        - error: Swift networking event error message.
        - NodeName: Name of the node.
        - PodId: Id of the swift load balancer pod.
        - PodName: Name of the swift load balancer pod.
        - ContainerName: Pod container name. There can be multiple containers in a pod.
        - ContainerImage: The docker image used by the pod container.
        - caller: event caller.
        """
        )]
        public Task<string> GetSwiftNetworkingEvents(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Managed Cluster Name of the container app.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetSwiftNetworkingEvents", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName }
            });
        }

        [Description("""
        Retrieves Envoy pods status in a Container Apps managed environment.    
        Use this tool when checking the health and status of Envoy pods in a Container Apps managed environment.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
        Tool outputs:
        - StartTime: Start time of the current envoy pod status.
        - EndTime: End time of the current envoy pod status.
        - PodName: Name of the envoy pod.
        - NodeName: Name of the node where the envoy pod is running.
        - PodStatus: Status of the envoy pod.
        - restartCount: Number of times the envoy pod has been restarted.
        - ContainerName: Pod container name. There can be multiple containers in a pod.
        - ContainerState: Status of the pod container. The value can be Ready or NotReady. If the value is NotReady, even if the pod is in running state, it indicates that the pod container is not ready to serve traffic.
        - ContainerImage: The docker image used by the container.
        """
        )]
        public Task<string> GetEnvoyPodStatus(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Managed Cluster Name of the container app.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
            new Dictionary<string, string>
            {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "podNamePrefix", "k8se-envoy" },
                { "podNamespace", "k8se-system" }
            });
        }

        [Description("""
        Retrieves pod status information for a specified Container App.            
        Use this tool when checking the health and status of a Container App.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
                    
        Tool outputs:
        - StartTime: Start time of the Container App pod status.
        - EndTime: End time of the Container App pod status.
        - PodName: Name of the Container App pod.
        - NodeName: Name of the node where the envoy pod is running.
        - PodStatus: Status of the Container App pod.
        - restartCount: Number of times the Container App pod has been restarted.
        - ContainerName: Pod container name. There can be multiple containers in a pod.
        - ContainerStatus: Status of the pod container. The value can be Ready or NotReady. If the value is NotReady, even if the pod is in running state, it indicates that the pod container is not ready to serve traffic.
         ContainerImage: The docker image used by the container.
        """
        )]
        public Task<string> GetContainerAppPodStatus(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Managed Cluster Name of the container app.")] string managedClusterName,
            [Description("Name of the container app.")] string containerAppName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetPodHealthStatus", region,
            new Dictionary<string, string>
            {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "podNamePrefix", containerAppName },
                { "podNamespace", "k8se-apps" }
            });
        }

        [Description("""
        Retrieves the provisioning status and operation details for a specified Container App.
                    
        Use this tool when checking the Container App provisioning status, and operation details.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
        Tool outputs:
        - StartTime: Start time of the current container app provisioning status.
        - EndTime: End time of the current container app provisioning status.
        - containerAppName: Name of the container app.
        - operationType: Operation type.
        - provisioningState: Provisioning status of the container app.
        """
        )]
        public Task<string> GetContainerAppStatus(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppStatus", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "subscriptionId", subscriptionId },
                { "resourceGroupName", resourceGroupName },
                { "containerAppName", containerAppName }
            });
        }

        [Description("""
        Retrieves admin events for a specified Container App.
        Use this tool when investigating Container App administrative operations, API calls, or troubleshooting deployment and configuration changes.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
                    
        Tool outputs:
            - PreciseTimeStamp: Container app admin event timestamp.
            - requestMethod: HTTP request method.
            - requestPath: HTTP request path.
            - statusCode: HTTP response status code.
            - requestBody: HTTP request body.
            - durationInMilliseconds: The duration of the request in milliseconds.
            - env_dt_traceId: The trace ID associated with the event.
        """
        )]
        public Task<string> GetContainerAppAdminEvents(
            [Description("Azure region.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppAdminEvents", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "subscriptionId", subscriptionId },
                { "resourceGroupName", resourceGroupName },
                { "resourceName", containerAppName }
            });
        }
    }
}
