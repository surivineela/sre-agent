// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using Agent.Core.Helpers;
using Agent.Plugins.Models;
using Microsoft.SemanticKernel;

namespace Agent.Plugins.Definitions
{
    public class RemediationPluginDefinition : IRemediationPlugin
    {
        private IRemediationPlugin _remediationPlugin;
        public RemediationPluginDefinition(IRemediationPlugin remediationPlugin)
        {
            _remediationPlugin = remediationPlugin;
        }

        [KernelFunction("calculate_scaling_cost")]
        [Description("Calculates the cost difference between current and target SKUs")]
        public async Task<RemediationResult> CalculateScalingCost(
            [Description("The resource ID of the App Service.")]
            string resourceId,
            [Description("Direction of scaling - 'up' or 'down'")]
            string direction,
            [Description("Current SKU of the app service plane")]
            string currentSku,
            [Description("Possible new sku of the app service plan")]
            string targetSku)
        {
            return await _remediationPlugin.CalculateScalingCost(resourceId, direction, currentSku, targetSku);
        }

        [KernelFunction("collect_memory_dump")]
        [Description("Collect memory dump from an App Service experiencing memory leaks for analysis.")]
        public async Task<RemediationResult> CollectMemoryDump(
            [Description("The resource ID of the App Service.")]
            string resourceId)
        {
            return await _remediationPlugin.CollectMemoryDump(resourceId);
        }

        [KernelFunction("restart_webapp")]
        [Description("Restart a Web App instance to mitigate memory leaks. This is typically used after scaling up " +
               "if memory issues persist. The restart will clear the memory and start fresh.")]
        public async Task<RemediationResult> RestartWebApp(
            [Description("The resource ID of the App Service.")]
            string resourceId)
        {
            return await _remediationPlugin.RestartWebApp(resourceId);
        }

        [KernelFunction("scale_app_service_plan_vertically")]
        [Description("Scale up an App Service Plan to a higher tier. SHOULD be always suggested when experiencing memory leaks. Prioritizes Premium v2/v3 tiers for better memory allocation.A scale up operation would incur a cost increase similarly a scale down operation would save costs, customer must be notified.")]
        public async Task<RemediationResult> ScaleAppServicePlanVertically(
            [Description("The resource ID of the App Service.")]
            string resourceId)
        {
            return await _remediationPlugin.ScaleAppServicePlanVertically(resourceId);
        }

        [KernelFunction("storage_account_set_shared_key_state")]
        [Description("Enables or disables the use of shared keys for accessing storage accounts. This controls whether callers are forced to use Managed Identities or Delegated Secure Access Token (SAS).")]
        public async Task<RemediationResult> StorageAccountSetSharedKeySupport(string resourceId, FeatureState featureState)
        {
            return await _remediationPlugin.StorageAccountSetSharedKeySupport(resourceId, featureState);
        }

        [KernelFunction("storage_account_set_public_containers_access")]
        [Description("Enables or disables public access to blob containers in the storage account. This controls a security measure that prevents unauthorized access to blobs.")]
        public async Task<RemediationResult> StorageAccountSetContainerPublicAccess(string resourceId, FeatureState featureState)
        {
            return await _remediationPlugin.StorageAccountSetContainerPublicAccess(resourceId, featureState);
        }

        [KernelFunction("cosmosdb_set_local_auth_support")]
        [Description("Sets the key based local auth setting on cosmosdb accounts. This forces callers to use non key based authentication methods such as managed identities or service principals.")]
        public async Task<RemediationResult> CosmosDbSetLocalAuthSupport(string resourceId, FeatureState featureState)
        {
            return await _remediationPlugin.CosmosDbSetLocalAuthSupport(resourceId, featureState);
        }

        [KernelFunction("eventhub_set_local_auth_support")]
        [Description("Sets the key based local auth setting on event hub accounts. This forces callers to use non key based authentication methods such as managed identities or service principals.")]
        public async Task<RemediationResult> EventHubSetLocalAuthSupport(string resourceId, FeatureState featureState)
        {
            return await _remediationPlugin.EventHubSetLocalAuthSupport(resourceId, featureState);
        }

        [KernelFunction("servicebus_set_local_auth_support")]
        [Description("Sets the key based local auth setting on event hub accounts. This forces callers to use non key based authentication methods such as managed identities or service principals.")]
        public async Task<RemediationResult> ServiceBusSetLocalAuthSupport(string resourceId, FeatureState featureState)
        {
            return await _remediationPlugin.ServiceBusSetLocalAuthSupport(resourceId, featureState);
        }

        [KernelFunction("azure_sql_server_set_auth_support")]
        [Description("Sets the authentication on azure sql server, disabling or enabling local auth support. If disabled, this forces callers to use authentication methods such as managed identities or service principals.")]
        public async Task<RemediationResult> AzureSqlServerSetLocalAuthSupport(string resourceId, FeatureState featureState)
        {
            return await _remediationPlugin.AzureSqlServerSetLocalAuthSupport(resourceId, featureState);
        }

        [KernelFunction("possible_next_sku")]
        [Description("Given a current sku suggest a possible next sku")]
        public async Task<RemediationResult> SuggestNextSku(
            [Description("The resource ID of the App Service.")]
            string resourceId,
            [Description("Direction of scaling - 'up' or 'down'")]
            string direction,
            [Description("Current SKU of the app service plan")]
            string currentSku)
        {
            return await _remediationPlugin.SuggestNextSku(resourceId, direction, currentSku);
        }
    }
}
