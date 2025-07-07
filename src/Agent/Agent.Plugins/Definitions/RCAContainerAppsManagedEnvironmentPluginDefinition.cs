using System.ComponentModel;
using Agent.Core.Interfaces;
using Agent.Framework;
using Agent.Plugins.Kusto;
using Agent.Core.Interfaces;
using Microsoft.SemanticKernel;
using Agent.Plugins.Interface;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppsManagedEnvironmentPluginDefinition
    {
        private readonly IKustoPluginChat _kustoPlugin;
        private readonly IAgentOutboundCommunicationService _agentOutboundCommunicationService;

        public RCAContainerAppsManagedEnvironmentPluginDefinition(IKustoPluginChat kustoPlugin, IAgentOutboundCommunicationService agentOutboundCommunicationService)
        {
            _kustoPlugin = kustoPlugin;
            _agentOutboundCommunicationService = agentOutboundCommunicationService;
        }

        [Description(@"""
Retrieve the base configuration info for an Azure Container Apps managed environment.
Use this tool when you need to gather detailed configuration information for a managed environment.
The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
Tool Output:
- Region
- environmentCreatedTime
- environmentSubscription: subscription of the managedEnvironment
- environmentResourceGroup
- environmentName
- environmentType: V1/V2
- isLegionEnabled: Whether the V2 environment is using the consumption workload
- isInternal 
- hasCustomerVnetForEnv
- hasCustomerVnetForCluster
- hasPrivateEndpoints 
- hasMaintenanceConfiguration 
- managedClusterCreatedTime
- managedSubscription: subscription of the managedCluster
- managedClusterName
- customHelmValues
- tier: standard/free
- currentChartVersion 
- targetChartVersion
- currentKubernetesVersion 
- targetKubernetesVersion
- loadBalancerResourceUrl
"""
)]
        public async Task<string> GetManagedEnvironmentConfigureInfo(
    [Description("Azure region of the managed environment.")] string region,
    [Description("Start time of the query.")] DateTime fromDate,
    [Description("End time of the query.")] DateTime toDate,
    [Description("Name of the managed environment.")] string environmentName,
    [Description("Name of the resource group.")] string resourceGroupName,
    [Description("Azure subscription ID.")] string subscriptionId,
    [Description("Name of the managed cluster")] string managedCluster)
        {
            // We use All("ManagedEnvironmentDBState") in the query, so if the region is not specified, we can default to an arbitrary region.
            string kustoClientRegion = string.IsNullOrEmpty(region)
                ? "centralus"
                : region;

            string environments =  await _kustoPlugin.ExecuteLocalFunctionAsync("GetManagedEnvironment", kustoClientRegion,
                 new Dictionary<string, string> {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "region", region },
                    { "environmentName", environmentName },
                    { "resourceGroupName", resourceGroupName },
                    { "subscriptionId", subscriptionId },
                    { "managedClusterName", managedCluster } 
                 }
             );
            return environments;
        }

        [Description(@"""
Retrieve the status of an Azure Container Apps managed environment.
Use this tool when you need to check the current status of a managed environment, including its provisioning state, power state, etc.
The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.

Tool Output:
- environmentProvisioningState: Current provisioning state of the managed environment
- environmentDeploymentErrors: Error details if environment deployment failed
- managedClusterProvisioningState: Current provisioning state of the managed cluster
- managedClusterProvisioningError: Error details if managed cluster provisioning failed
- powerState: Current power state of the managed environment (e.g., Running, Suspended)
- targetPowerState: Target power state of the managed environment
- chartVersionUpgradeErrors: Errors related to chart version upgrades
- kubernetesVersionUpgradeErrors: Errors related to Kubernetes version upgrades
"""
)]
        public async Task<string> GetManagedEnvironmentStateInfo(
            [Description("Azure region of the managed environment.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed environment.")] string environmentName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId,
            [Description("provide sampling inputs")] SamplingOptions sampling)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetManagedEnvironmentStatus", region,
                 new Dictionary<string, string> {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "region", region },
                    { "environmentName", environmentName },
                    { "resourceGroupName", resourceGroupName },
                    { "subscriptionId", subscriptionId }
                 }
             );
        }

        [Description(@"""
        Retrieve configuration state changes for a specific Azure Container Apps managed environment within a given time range.
        Use this tool when you need to track changes in the managed environment's configuration, such as chart versions, Kubernetes versions, and workload profiles.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
        Tool output:
        - StartTime: Timestamp when the change was detected.
        - EndTime: Timestamp when the change was completed.
        - ComponentType: Type of the component that was changed (e.g., 'Chart Version', 'Kubernetes Version', 'Has Workload Profiles').
        - Value: The value of the component after the change.
        - ChangeStatus: Status of the change (e.g., 'No change', 'Change').
        - PreviousValue: The value of the component before the change.
        """
        )]
        public async Task<string> GetChangesInManagedEnvironment(
            [Description("Azure region of the managed environment.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Customer subscription ID of the managed environment.")] Guid customerSubscriptionId,
            [Description("Name of the customer managed environment.")] string managedEnvironmentName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync(
                "GetChangesinManagedEnvironment",
                region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "customerSubscriptionId", customerSubscriptionId.ToString() },
                    { "managedEnvironmentName", managedEnvironmentName }
                });
        }

        [Description(@"""
        Generate a direct ASI (App Service Insights) page URL for a given Azure Container Apps managed environment.
        Use this tool when you need to provide the user with a direct link to the ASI page of a Container Apps environment.
        
        Tool output:
        - A formatted string containing the ASI diagnostic URL that can be clicked to access the managed environment ASI page.
        """)]
        public Task<string> GetASIPageForManagedEnvironment(
    [Description("Azure region of the managed environment.")] string region,
    [Description("Start time of the query.")] DateTime fromDate,
    [Description("End time of the query.")] DateTime toDate,
    [Description("Name of the managed environment.")] string environmentName,
    [Description("Name of the resource group.")] string resourceGroupName,
    [Description("Azure subscription ID.")] string subscriptionId)
        {
            var basePath = "/services/ACA Azure Container Apps/pages/Container App Environment";

            var cleanPath = Uri.EscapeDataString(basePath); // encodes spaces etc.

            var query = $"environmentLocation={Uri.EscapeDataString(region.ToLowerInvariant())}" +
               $"&environmentName={Uri.EscapeDataString(environmentName)}" +
               $"&environmentResourceGroup={Uri.EscapeDataString(resourceGroupName)}" +
               $"&environmentSubscription={Uri.EscapeDataString(subscriptionId)}" +
               $"&globalFrom={Uri.EscapeDataString(fromDate.ToString("M/d/yyyy hh:mm:ss tt"))}" +
               $"&globalTo={Uri.EscapeDataString(toDate.ToString("M/d/yyyy hh:mm:ss tt"))}";

            var adxUri = $"https://asi.azure.ms{cleanPath}?{query}";

            return Task.FromResult($"ASI Page for managed environment {adxUri}");
        }

        [Description(@"""
        Retrieves the Azure Container Apps environment resource identity based on the managed cluster name.
        Use this tool when you need to map from a managed cluster name to its associated Container Apps environment resource identity.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
        Tool outputs:
        - TIMESTAMP: The timestamp of when the resource identity information was retrieved.
        - managedClusterName: Name of the managed cluster.
        - subscription: Azure subscription ID of the managed environment.
        - resourceGroup: Resource group of the managed environment.
        - managedEnvironmentName: Name of the managed environment.
        """
        )]
        public async Task<string> GetManagedClusterEnvironmentResourceId(
            [Description("Azure region of the managed environment.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            if (string.IsNullOrEmpty(managedClusterName))
            {
                throw new ArgumentException("Managed cluster name cannot be null or empty.", nameof(managedClusterName));
            }

            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetManagedClusterEnvironmentResourceId", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }

        [Description(@"""
Retrieve the provisioning status history of a specific Azure Container Apps managed environment.
Use this tool when you need to track the provisioning state changes of a managed environment over time or investigate issues related to environment deployment or provisioning.
The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.

Tool outputs:
- StartTime: Timestamp when this provisioning state began
- EndTime: Timestamp when this provisioning state ended
- environmentProvisioningState: Current state of the environment (e.g., Succeeded, Failed, ScheduledForDelete)
- powerState: Current power state of the environment (e.g., Running, Suspended, UpdateRequested)
- managedClusterName: Name of the underlying managed cluster
- environmentDeploymentErrors: Error details if environment deployment failed
- managedClusterProvisioningError: Error details if managed cluster provisioning failed
        """
        )]
        public async Task<string> GetManagedEnvironmentProvisioningStatus(
            [Description("Azure region of the managed environment.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed environment.")] string environmentName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetManagedEnvironmentProvisioningStatus", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "environmentName", environmentName },
                    { "resourceGroupName", resourceGroupName },
                    { "subscriptionId", subscriptionId }
                });
        }

        [Description(@"""
        Retrieve Azure Container Apps environment admin operation events for troubleshooting and auditing purposes.
        Use this tool when you need to analyze administrative API calls, their outcomes, and performance metrics.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
        Tool outputs:
        - PreciseTimeStamp: Timestamp of the event.
        - requestPath: The path of the admin request.
        - requestMethod: The HTTP method used for the request.
        - statusCode: The status code returned by the request.
        - requestBody: The body of the request.
        - durationInMilliseconds: The duration of the request in milliseconds.
        - env_dt_traceId: The trace ID associated with the event.
        """
        )]
        public async Task<string> GetManagedEnvironmentAdminEvents(
            [Description("Azure region of the managed environment.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed environment.")] string environmentName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetContainerAppAdminEvents", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "resourceName", environmentName },
                    { "resourceGroupName", resourceGroupName },
                    { "subscriptionId", subscriptionId }
                });
        }

        [Description(@"""
        Retrieve the Azure Container Apps environment operation errors for troubleshooting and incident analysis.
        Use this tool when you need to identify errors or failures in the environment operations.
        The tool returns a timeline of errors grouped by type, with frequency counts and detailed exception messages.
        
        Tool outputs:
        - FirstSeen: Timestamp of the first occurrence of the error.
        - LastSeen: Timestamp of the last occurrence of the error.
        - count: The number of times the error has occurred.
        - operationType: The type of operation that caused the error (e.g., UpdateEnvironmentChart, InstallComponents, Delete).
        - operationEntityType: The type of entity that the operation was performed on.
        - exception: The exception message associated with the error, providing details about the failure reason.
        """
        )]
        public async Task<string> GetManagedEnvironmentOperationErrors(
            [Description("Azure region of the managed environment.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed environment.")] string environmentName,
            [Description("Name of the resource group.")] string resourceGroupName,
            [Description("Azure subscription ID.")] string subscriptionId)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetManagedEnvironmentOperationErrors", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "environmentName", environmentName },
                    { "resourceGroupName", resourceGroupName },
                    { "subscriptionId", subscriptionId }
                });
        }

        [Description(@"""
Retrieve the managed cluster private endpoint connection details.
Use this tool to examine the connection details of private endpoint connections for a managed cluster.
The tool returns table data in CSV format, using TAB separators.The first line contains the column headers.

Tool outputs:
- Timestamp: Event retrieval time
- frontendVmssName: Name of the frontend VMSS
- frontendVmssCreatedTime: Creation time of the frontend VMSS
- frontendVmssProvisioningState: Provisioning state of the frontend VMSS
- tcpBridgeVersion: Version of the TCP bridge
- privateEndpointConnectionName: Name of the private endpoint connection
- privateEndpointConnectionProxyName: Name of the private endpoint connection proxy
- privateEndpointId: Azure resource ID of the private endpoint
- privateEndpointConnectionProvisioningState: Provisioning state of the private endpoint connection
- connectionStatus: Current status of the connection
- storageAccountName: Name of the associated storage account
"""
)]
        public async Task<string> GetPrivateEndpointConnectionDetails(
            [Description("Azure region of the managed environment.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetPrivateEndpointConnectionDetails", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                });
        }

        [Description(@"""
        Retrieve the Private Endpoint Connection connection state details.
        Use this tool when you need to track or troubleshoot the connection status of a private endpoint connection over time.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
        Tool outputs:
        - StartTime: Start time of the reported connection state.
        - EndTime: End time of the reported connection state.
        - ConnectionState: The connection status of the private endpoint connection.
        """
        )]
        public async Task<string> GetPrivateEndpointConnectionConnectionState(
            [Description("Azure region of the managed environment.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the private endpoint connection.")] string privateEndpointConnectionName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetPEConnectionState", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "privateEndpointConnectionName", privateEndpointConnectionName }
                });
        }

        [Description(@"""
        Retrieve the Private Endpoint Connection Provisioning state details.
        Use this tool when you need to track or troubleshoot the provisioning lifecycle of a private endpoint connection.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.

        Tool outputs:
        - StartTime: Start time of the reported provisioning state.
        - EndTime: End time of the reported provisioning state.
        - ProvisioningState: The current provisioning state of the private endpoint connection.
        """
        )]
        public async Task<string> GetPrivateEndpointConnectionProvisioningState(
    [Description("Azure region of the managed environment.")] string region,
    [Description("Start time of the query.")] DateTime fromDate,
    [Description("End time of the query.")] DateTime toDate,
    [Description("Name of the private endpoint connection.")] string privateEndpointConnectionName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetPEProvisioningState", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "privateEndpointConnectionName", privateEndpointConnectionName }
                });
        }

        [Description(@"""
        Retrieve the provisioning state of the customer frontend VMSS (Virtual Machine Scale Set) for a specific Private EndpointConnection.
        Use this tool when you need to track or troubleshoot the provisioning lifecycle of the frontend VMSS associated with a private endpoint connection.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
                
        Tool outputs:
        - StartTime: Start time of the reported Provisioning status.
        - EndTime: End time of the reported Provisioning status.
        - ProvisioningState: The Provisioning state of the customer frontend VMSS.
        """
        )]
        public async Task<string> GetPrivateEndpointConnectionFrontendVmssProvisioningState(
            [Description("Azure region of the managed environment.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the frontend VMSS.")] string frontendVmssName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetPEFrontendVmssProvisioningState", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "frontendVmssName", frontendVmssName }
                });
        }

        [Description(@"""
        Retrieve logs by a trace ID(env_dt_traceId) to trace all events related to a specific Container App Admin Event.
        Use this tool when you need to investigate specific errors related to an Admin operation by its trace ID(env_dt_traceId).    
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.

        Tool outputs:
        - StartTime: First occurrence of the error message.
        - EndTime: Last occurrence of the error message.
        - Message: The operation error message.
        """
        )]
        public async Task<string> GetAdminEventErrorMessagesByTraceId(
            [Description("Azure region of the managed environment.")] string region,
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Trace ID to search for error messages.")] string traceId)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetAdminEventErrorMessagesByTraceId", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "env_dt_traceId", traceId }
                });
        }

        [Description(@"""
        Retrieve AKS node alerts and their status over time for a specific managed cluster.
        Use this tool when troubleshooting node issues in the Azure Kubernetes Service cluster that hosts a Container Apps environment.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.
        
        Tool outputs:
        - StartTime: Start time of the alert timeline.
        - EndTime: End time of the alert timeline.
        - Message: Description of alert status (e.g., 'Healthy' or 'X Alerts').
        - Tooltip: Detailed information about critical and warning alerts.
        - HealthStatus: Overall health status ('healthy', 'degraded', 'error').
        - Area: Alert categorization (e.g., 'Alerts: Node').
        - WarningErrors: List of warning-level alerts.
        - CriticalErrors: List of critical-level alerts.
        """
        )]
        public async Task<string> GetAKSNodeAlerts(
            [Description("Start time of the query.")] DateTime fromDate,
            [Description("End time of the query.")] DateTime toDate,
            [Description("Name of the managed cluster.")] string managedClusterName)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetAKSNodeAlerts", "centralus",
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "managedClusterName", managedClusterName }
                },
                groupName: "AKS");
        }

        [Description(@"""
        Retrieve logs by a correlation ID to trace all events related to a specific request or operation.
        Use this tool when you need to investigate the complete lifecycle of a request across different components or troubleshoot issues by following a specific correlation ID.
        The tool returns table data in CSV format, using TAB separators. The first line contains the column headers.

        Tool outputs:
        - PreciseTimeStamp: the exact time the operation was logged,
        - message: the content of the operation log,
        - severityText: the message level (Information/Warning/Error),
        - requestMethod: the HTTP method of the operation,
        - requestPath: the path of the operation request,
        - statusCode: the HTTP response code,
        - exception: exception message,
        - env_dt_traceId: unique identifier for tracing the complete lifecycle of a request
        """
         )]
        public async Task<string> GetLogsByCorrelationId(
    [Description("Azure region of the managed environment.")] string region,
    [Description("Start time of the query.")] DateTime fromDate,
    [Description("End time of the query.")] DateTime toDate,
    [Description("Correlation ID to filter the operation logs. This parameter cannot be empty")] string correlationId)
        {
            if (string.IsNullOrEmpty(correlationId))
            {
                throw new ArgumentException("Correlation ID cannot be null or empty.", nameof(correlationId));
            }

            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetLogsByCorrelationId", region,
                new Dictionary<string, string>
                {
                    { "fromDate", fromDate.ToString() },
                    { "toDate", toDate.ToString() },
                    { "correlationId", correlationId }
                });
        }
    }
}
