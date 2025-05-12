using System.ComponentModel;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Core.Plugins.Interfaces;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Core.Plugins.Definitions
{
    public class ContainerAppEnvoyPluginDefinition
    {
        private readonly IContainerAppEnvoyPlugin _plugin;
        public ContainerAppEnvoyPluginDefinition(IContainerAppEnvoyPlugin Plugin)
        {
            _plugin = Plugin;
        }

        [KernelFunction(KernelFunctionNames.ACA.GetContainerAppManagedClusterName)]
        [Description(@"Retrieve Container Apps Managed Cluster
Projects:
    - managedClusterName: Managed Cluster Name of the given Container App.
")]
        public Task<string> GetContainerAppManagedCluster([Description("Azure region.")] string region, [Description("Start time of the query.")] DateTime fromDate, [Description("End time of the query.")] DateTime toDate, [Description("Name of the container app.")] string containerAppName, [Description("Name of the resource group.")] string resourceGroupName, [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _plugin.GetContainerAppManagedCluster(region, fromDate, toDate, containerAppName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetEnvoyAbnormalLogs)]
        [Description(@"Retrieve Container Apps Envoy Abnormal Logs. 
Projects:
  - EnvironmentName: Environment name, also called Managed Cluster Name.
  - Log: Envoy container abnormal log.
  - Role: Cluster Node Id.
  - _ContainerGroupId: Envoy container group Id.
  - _ContainerGroupName: Envoy container group Name.
  - _ContainerId: Envoy container Id.
  - _ContainerImage: The docker image used by the Envoy container.
")]
        public Task<string> GetEnvoyAbnormalLogs([Description("Azure region.")] string region, [Description("Start time of the query.")] DateTime fromDate, [Description("End time of the query.")] DateTime toDate, [Description("Managed Cluster Name of the container app.")] string managedClusterName)
        {
            return _plugin.GetEnvoyAbnormalLogs(region, fromDate, toDate, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetEnvoyControllerLogs)]
        [Description(@"Retrieve Container Apps Envoy Controller Logs.
Projects:
  - PreciseTimeStamp: Envoy controller log timestamp.
  - Log: Envoy controller events log.
  - msg: Envoy controller events message.
  - error: Envoy controller events error message.
  - Role: Cluster Node Id.
  - _ContainerGroupId: Envoy container group Id.
  - _ContainerGroupName: Envoy container group Name.
  - _ContainerId: Envoy container Id.
  - _ContainerImage: The docker image used by the Envoy container.
  - caller:
")]
        public Task<string> GetEnvoyControllerLogs([Description("Azure region.")] string region, [Description("Start time of the query.")] DateTime fromDate, [Description("End time of the query.")] DateTime toDate, [Description("Managed Cluster Name of the container app.")] string managedClusterName)
        {
            return _plugin.GetEnvoyControllerLogs(region, fromDate, toDate, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetEnvoyAccessLogs)]
        [Description(@"Retrieve Container Apps Envoy Access Logs
Projects:
  - FirstSeen: Start time of the current kind of envoy access log.
  - LastSeen: End time of the current kind of envoy access log.
  - max_RequestDuration: maximum request duration of this kind of envoy access log.
  - Count: count of this kind of envoy access log.
  - Authority: Request access domain name.
  - Method: HTTP request methods.
  - Path: Request access path.
  - Protocol: Internet protocol.
  - Status: HTTP response status(e.g., 200, 503).
  - ResponseCodeDetails: Response code details. (e.g. via_upstream, downstream_remote_disconnect)
  - UpstreamHost: The upstream host's IP address and port in the format <ip-address>:<port> (e.g., 100.100.202.85:8080)..
")]
        public Task<string> GetEnvoyAccessLogs([Description("Azure region.")] string region, [Description("Start time of the query.")] DateTime fromDate, [Description("End time of the query.")] DateTime toDate, [Description("Managed Cluster Name of the container app.")] string managedClusterName)
        {
            return _plugin.GetEnvoyAccessLogs(region, fromDate, toDate, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetSwiftNetworkingEvents)]
        [Description(@"Retrieve Swift Networking Events
Projects:
  - PreciseTimeStamp: Swift networking event timestamp.
  - logger: Swift networking event logger.
  - Log: Swift networking event log.
  - msg: Swift networking event message.
  - error: Swift networking event error message.
  - Role: Cluster Node Id.
  - _ContainerGroupId: Envoy container group Id.
  - _ContainerGroupName: Envoy container group Name.
  - _ContainerId: Envoy container Id.
  - _ContainerImage: The docker image used by the Envoy container.
  - caller: event caller.
")]
        public Task<string> GetSwiftNetworkingEvents([Description("Azure region.")] string region, [Description("Start time of the query.")] DateTime fromDate, [Description("End time of the query.")] DateTime toDate, [Description("Managed Cluster Name of the container app.")] string managedClusterName)
        { 
            return _plugin.GetSwiftNetworkingEvents(region, fromDate, toDate, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetEnvoyPodStatus)]
        [Description(@"Retrieve Envoy Pod Status
Projects:
- StartTime: Start time of the current envoy pod status.
- EndTime: End time of the current envoy pod status.
- PodName: Name of the envoy pod.
- PodStatus: Status of the envoy pod.
")]
        public Task<string> GetEnvoyPodStatus([Description("Azure region.")] string region, [Description("Start time of the query.")] DateTime fromDate, [Description("End time of the query.")] DateTime toDate, [Description("Managed Cluster Name of the container app.")] string managedClusterName)
        {
            return _plugin.GetEnvoyPodStatus(region, fromDate, toDate, managedClusterName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetCustomerAppPodStatus)]
        [Description(@"Retrieve Customer Application Pod Status
Projects:
- StartTime: Start time of the customer application pod status.
- EndTime: End time of the customer application pod status.
- PodName: Name of the customer application pod.
- PodStatus: Status of the customer application pod.
")]
        public Task<string> GetCustomerAppPodStatus([Description("Azure region.")] string region, [Description("Start time of the query.")] DateTime fromDate, [Description("End time of the query.")] DateTime toDate, [Description("Managed Cluster Name of the container app.")] string managedClusterName, [Description("Name of the container app.")] string containerAppName)
        {
            return _plugin.GetCustomerAppPodStatus(region, fromDate, toDate, managedClusterName, containerAppName);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetContainerAppStatus)]
        [Description(@"Retrieve Container App Status
Projects:
- StartTime: Start time of the current container app provisioning status.
- EndTime: End time of the current container app provisioning status.
- containerAppName: Name of the container app.
- operationType: Operation type.
- provisioningState: Provisioning status of the container app.
")]
        public Task<string> GetContainerAppStatus([Description("Azure region.")] string region, [Description("Start time of the query.")] DateTime fromDate, [Description("End time of the query.")] DateTime toDate, [Description("Name of the container app.")] string containerAppName, [Description("Name of the resource group.")] string resourceGroupName, [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _plugin.GetContainerAppStatus(region, fromDate, toDate, containerAppName, resourceGroupName, subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetContainerAppAdminEvents)]
        [Description(@"Retrieve Container App Admin Events
Projects:
- PreciseTimeStamp: Container app admin event timestamp.
- requestMethod: HTTP request method.
- requestPath: HTTP request path.
- statusCode: HTTP response status code.
- requestBody: HTTP request body.
")]
        public Task<string> GetContainerAppAdminEvents([Description("Azure region.")] string region, [Description("Start time of the query.")] DateTime fromDate, [Description("End time of the query.")] DateTime toDate, [Description("Name of the container app.")] string containerAppName, [Description("Name of the resource group.")] string resourceGroupName, [Description("Azure subscription ID.")] string subscriptionId)
        {
            return _plugin.GetContainerAppAdminEvents(region, fromDate, toDate, containerAppName, resourceGroupName, subscriptionId);
        }
    }
}
