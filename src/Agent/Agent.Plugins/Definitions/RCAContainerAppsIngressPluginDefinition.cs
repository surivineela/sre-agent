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
        Purpose:
        Retrieves managed cluster details for a specified Container App.

        Scenario:
        Use this tool when you need to identify the underlying managed cluster of a Container App to troubleshoot connectivity or configuration issues.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - managedClusterName: Managed Cluster Name of the given Container App
        - environmentType: Environment type of the Container App (V1 or V2)
        - hasCustomerVnetForEnv: Whether the Container App has a customer VNet configured for the environment
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
        Purpose:
        Retrieves ingress configuration details for a specified Container App.

        Scenario:
        Use this tool when you need to determine how a Container App can be accessed.
        Determine whether the app is reachable from:
        - the public internet,
        - within a VNET associated with its managed environment, or
        - only other clients within the same managed environment.
      
        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - StartTime: Start time of the current ingress configuration
        - EndTime: End time of the current ingress configuration
        - IngressEnabled: Whether ingress is enabled for the Container App
        - IsInternalApp: Whether the app is configured for accept traffic from anywhere(IsInternalApp=false), or limit it to traffic from within the same Container Apps managed environment(IsInternalApp=true)
        - IsInternalEnvironment: Whether the managed environment is configured for internet access or limited to a virtual network
        """
        )]
        public Task<string> GetContainerAppIngressConfig(
            [Description("Azure region of the container app.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the container app.")] string containerAppName,
            [Description("Managed Cluster Name of the container app.")] string managedClusterName)
        {
            return _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppIngressConfig", region,
            new Dictionary<string, string> {
                { "fromDate", fromDate.ToString() },
                { "toDate", toDate.ToString() },
                { "managedClusterName", managedClusterName },
                { "containerAppName", containerAppName }
            });
        }

        [Description("""
        Purpose:
        Retrieves Envoy pod logs from a specified Managed Cluster of container app.

        Scenario:
        Use this tool when diagnosing Envoy pod issues, such as connectivity failures, routing problems, or unexpected envoy pod behavior.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - TimeStamp: UTC Time of the log event
        - NodeName: Name of the node running the Envoy pod
        - PodName: Name of the Envoy pod
        - Log: Content of the Envoy pod log entry
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
        Purpose:
        Retrieves time series data of Container Apps Envoy access requests grouped by HTTP status codes at the Container App level.

        Scenario:
        Use this tool to get an overview of the requests received by Envoy in a Container App, categorized by HTTP status codes.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - TimeStamp: Timestamp of the envoy access request
        - Count: Count of envoy access requests
        - seriesName: Name of the envoy access request series (e.g., Http 2xx Count, Http 3xx Count, Http 4xx Count, Http 5xx Count)
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
        Purpose:
        Retrieves time series data of Container Apps Envoy access requests at the managed cluster level.

        Scenario:
        Use this tool when analyzing overall traffic patterns, monitoring request volumes across the entire managed cluster.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - TimeStamp: Timestamp of the envoy access request
        - Count: Count of envoy access requests
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
        Purpose:
        Retrieves detailed Container Apps Envoy Access Logs.

        Scenario:
        Use this tool when analyzing detailed Container Apps Envoy access logs, such as identifying specific request patterns, troubleshooting issues with specific requests.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - FirstSeen: Start time of the current kind of envoy access log
        - LastSeen: End time of the current kind of envoy access log
        - max_RequestDuration: Maximum request duration of this kind of envoy access log
        - Count: Count of this kind of envoy access log
        - Authority: Request access domain name
        - Method: HTTP request methods
        - Path: Request access path
        - Protocol: Internet protocol
        - Status: HTTP response status (e.g., 200, 503)
        - ResponseCodeDetails: Response code details (e.g., via_upstream, downstream_remote_disconnect)
        - UpstreamHost: The upstream host's IP address and port (e.g., 100.100.202.85:8080)
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
        Purpose:
        Retrieves Swift Networking Events from the Container Apps managed environment.

        Scenario:
        Use this tool when troubleshooting Swift networking connectivity issues, analyzing configuration problems, or swift networking events failures.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - PreciseTimeStamp: Swift networking event timestamp
        - logger: Swift networking event logger
        - LogMessage: Swift networking event log message
        - error: Swift networking event error message
        - NodeName: Name of the node
        - PodId: Id of the swift load balancer pod
        - PodName: Name of the swift load balancer pod
        - ContainerName: Pod container name
        - ContainerImage: The docker image used by the pod container
        - caller: event caller
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
        Purpose:
        Retrieves Envoy pods status in a Container Apps managed environment.

        Scenario:
        Use this tool when checking the health and status of Envoy pods in a Container Apps managed environment.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - StartTime: Start time of the current envoy pod status
        - EndTime: End time of the current envoy pod status
        - PodName: Name of the envoy pod
        - NodeName: Name of the node where the envoy pod is running
        - PodStatus: Status of the envoy pod
        - restartCount: Number of times the envoy pod has been restarted
        - ContainerName: Pod container name
        - ContainerState: Status of the pod container (Ready or NotReady)
        - ContainerImage: The docker image used by the container
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
        Purpose:
        Retrieves pod status information for a specified Container App.

        Scenario:
        Use this tool when troubleshooting HTTP errors to determine if they're caused by container app pod issues, such as unhealthy pods or pod container failures.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - StartTime: Start time of the Container App pod status
        - EndTime: End time of the Container App pod status
        - PodName: Name of the Container App pod
        - NodeName: Name of the node where the envoy pod is running
        - PodStatus: Status of the Container App pod
        - restartCount: Pod restart count
        - ContainerName: Pod container name
        - ContainerStatus: Status of the pod container (Ready or NotReady)
        - ContainerImage: The docker image used by the container
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
        Purpose:
        Retrieves the provisioning status and operation details for a specified Container App.

        Scenario:
        Use this tool when troubleshooting HTTP errors or ingress failures to determine if they're caused by container app unhealthy provisioning states, failed deployments, or operation issues.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - StartTime: Start time of the current container app provisioning status
        - EndTime: End time of the current container app provisioning status
        - containerAppName: Name of the container app
        - operationType: Operation type
        - provisioningState: Provisioning status of the container app
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
        Purpose:
        Retrieves admin events for a specified Container App.

        Scenario:
        Use this tool when you need to check the ingress log missing events, http errors, or ingress failures were caused by administrative operations, API calls, or deployment and configuration changes.

        Output:
        Returns table data in CSV format with TAB separators. Column headers:
        - PreciseTimeStamp: Container app admin event timestamp
        - requestMethod: HTTP request method
        - requestPath: HTTP request path
        - statusCode: HTTP response status code
        - requestBody: HTTP request body
        - durationInMilliseconds: The duration of the request in milliseconds
        - env_dt_traceId: The trace ID associated with the event
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
