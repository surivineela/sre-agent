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

        [Description(
        @"Retrieve the base information, configuration, and state for an Azure Container Apps managed environment.
Tool Output:
Region,
environmentCreatedTime,
environmentSubscription: subscription of the managedEnvironment,
environmentResourceGroup,
environmentName,
environmentType: V1/V2,
environmentProvisioningState,
environmentDeploymentErrors,
isLegionEnabled: Whether the V2 environment is using the consumption workload,
isInternal,
hasCustomerVnet,
hasPrivateEndpoints,
hasMaintenanceConfiguration,
managedClusterCreatedTime,
managedSubscription: subscription of the managedCluster,
managedClusterName,
customHelmValues,
tier,
managedClusterProvisioningState,
managedClusterProvisioningError,
powerState,
targetPowerState,
currentChartVersion,
targetChartVersion: chart version that should be updated to,
chartVersionUpgradeErrors,
currentKubernetesVersion,
targetKubernetesVersion: Kubernetes version that should be updated to,
kubernetesVersionUpgradeErrors,
loadBalancerResourceUrl,
"
)]
        public async Task<string> GetManagedEnvironmentInfo(
    [Description("Azure region.")] string region,
    [Description("Start time of the query.")] DateTime fromDate,
    [Description("End time of the query.")] DateTime toDate,
    [Description("Name of the managed environment.")] string environmentName,
    [Description("Name of the resource group.")] string resourceGroupName,
    [Description("Azure subscription ID.")] string subscriptionId,
     [Description("provide sampling inputs")] SamplingOptions sampling)
        {
            return await _kustoPlugin.ExecuteLocalFunctionAsync("GetManagedEnvironment", region,
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

        [Description(
        @"Retrieve configuration state changes for a specific Azure Container Apps managed environment within a given time range.

        This function helps identify if incidents are correlated with configuration changes by highlighting changes that align with the reported issue timeline.

        **Returns a list of component types that are changed**, including their previous and current values during the specified period.
        Note: Unchanged components are NOT returned.
        "
        )]
        public async Task<string> GetChangesInManagedEnvironment(
            [Description("Azure region in lower case. example: 'westeurope'")] string region,
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

        [Description(
@"Retrieve a direct ASI (App Service Insights) page URL for a given Azure Container Apps managed environment.

Tool outputs:
- region: Azure region hosting the environment.
- environmentName: Name of the ACA managed environment.
- fromDate / toDate: Time window of interest.
- resourceGroupName: Resource group of the environment.
- subscriptionId: Azure subscription ID.
- ASI URL: Clickable diagnostic link for ACA platform health and metadata."
)]
        public Task<string> GetASIPageForManagedEnvironment(
    [Description("Azure region.")] string region,
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

        [Description(
        @"Retrieve the Azure Container Apps environment resource identity based on the managed cluster name.
        Tool outputs:
        - managedClusterName: Name of the managed cluster.
        - subscription: Azure subscription ID of the Azure Container Apps environment.
        - resourceGroup: Resource group of the Azure Container Apps environment.
        - environmentName: Name of the Azure Container Apps environment."
        )]
        public async Task<string> GetManagedClusterEnvironmentResourceId(
            [Description("Azure region.")] string region,
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

        [Description(
        @"Retrieve the provisioning status of a specific Azure Container Apps managed environment.
        Tool outputs:
        - StartTime: Start time of the reported environment provisioning status.
        - EndTime: End time of the reported environment provisioning status.
        - environmentProvisioningState
        - powerState
        - managedClusterName
        - environmentDeploymentErrors
        - managedClusterProvisioningError."
        )]
        public async Task<string> GetManagedEnvironmentProvisioningStatus(
            [Description("Azure region.")] string region,
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

        [Description(
        @"Retrieve the Azure Container Apps environment Admin operation events.
        Tool outputs:
        - PreciseTimeStamp: Timestamp of the event.
        - requestPath: The path of the request.
        - requestMethod: The HTTP method used for the request.
        - statusCode: The status code returned by the request.
        - requestBody: The body of the request.
        - durationInMilliseconds: The duration of the request in milliseconds.
        - env_dt_traceId: The trace ID associated with the event."
        )]
        public async Task<string> GetManagedEnvironmentAdminEvents(
            [Description("Azure region.")] string region,
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

        [Description(
        @"Retrieve the Azure Container Apps environment operation errors.
        Tool outputs:
        - FirstSeen: Timestamp of the first occurrence of the error.
        - LastSeen: Timestamp of the last occurrence of the error.
        - count: The number of times the error has occurred.
        - operationType: The type of operation that caused the error.
        - operationEntityType: The type of entity that the operation was performed on.
        - exception: The exception message associated with the error."
        )]
        public async Task<string> GetManagedEnvironmentOperationErrors(
            [Description("Azure region.")] string region,
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

        [Description(
@"Retrieve the Azure Container Apps managed cluster private endpoint connection details.
Tool outputs:
- frontendVmssName
- frontendVmssCreatedTime
- frontendVmssProvisioningState
- tcpBridgeVersion
- privateEndpointConnectionName
- privateEndpointConnectionProxyName
- privateEndpointId
- privateEndpointConnectionProvisioningState
- connectionStatus
- storageAccountName
        ")]
        public async Task<string> GetPrivateEndpointConnectionDetails(
            [Description("Azure region.")] string region,
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

        [Description(
@"Retrieve the Azure Container Apps Private Endpoint Connection connection state details.
Tool outputs:
- StartTime: Start time of the reported connection state.
- EndTime: End time of the reported connection state.
- ConnectionState: The connection status of the private endpoint connection.
")]
        public async Task<string> GetPrivateEndpointConnectionConnectionState(
            [Description("Azure region.")] string region,
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

        [Description(
@"Retrieve the Azure Container Apps Private Endpoint Connection Provisioning state details.
Tool outputs:
- StartTime: Start time of the reported Provisioning status.
- EndTime: End time of the reported Provisioning status.
- ProvisioningState: The Provisioning state of the private endpoint connection.
")]
        public async Task<string> GetPrivateEndpointConnectionProvisioningState(
    [Description("Azure region.")] string region,
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

        [Description(
@"Retrieve the provisioning state of the customer frontend VMSS (Virtual Machine Scale Set) for a specific Private Endpoint Connection.
Tool outputs:
- StartTime: Start time of the reported Provisioning status.
- EndTime: End time of the reported Provisioning status.
- ProvisioningState: The Provisioning state of the customer frontend VMSS.
")]
        public async Task<string> GetPrivateEndpointConnectionFrontendVmssProvisioningState(
            [Description("Azure region.")] string region,
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

        [Description(
@"Retrieve detailed error messages for Azure Container Apps environment Admin operation events. Every environment Admin event has a unique trace ID (env_dt_traceId) that can be used to correlate related events and errors.

Tool outputs:
- FirstSeen: First occurrence of the error message.
- LastSeen: Last occurrence of the error message.
- message: The error message content.
")]
        public async Task<string> GetAdminEventErrorMessagesByTraceId(
            [Description("Azure region.")] string region,
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

        [Description(
@"Retrieve AKS node alerts and their status over time for a specific managed cluster.
Tool outputs:
- StartTime: Start time of the alert timeline.
- EndTime: End time of the alert timeline.
- Content: Description of alert status (e.g., 'Healthy' or 'X Alerts').
- Tooltip: Detailed information about critical and warning alerts.
- Health: Overall health status ('healthy', 'degraded', 'error').
- GroupBy: Alert categorization (e.g., 'Alerts: Node').
- warnings: List of warning-level alerts.
- criticals: List of critical-level alerts."
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

        [Description(
        @"Retrieve logs by a correlation ID. This tool can help to trace all logs related to a specific request.
        Tool outputs:
        - PreciseTimeStamp: the exact time the operation was logged,
        - message: the content of the operation log,
        - severityText: the message level (Information/Warning/Error),
        - requestMethod: the HTTP method of the operation,
        - requestPath: the path of the operation request,
        - statusCode: the HTTP response code,
        - exception: exception message,
        - env_dt_traceId: unique identifier for tracing the complete lifecycle of a request"
)]
        public async Task<string> GetLogsByCorrelationId(
    [Description("Azure region.")] string region,
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
