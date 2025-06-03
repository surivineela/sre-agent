// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Models;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins.Definitions
{
    public class ContainerAppQuotaPluginDefinition
    {
        private readonly IContainerAppQuotaPlugin _plugin;

        public ContainerAppQuotaPluginDefinition(IContainerAppQuotaPlugin plugin)
        {
            _plugin = plugin;
        }

        [KernelFunction(KernelFunctionNames.ACA.GetSubscriptionQuota)]
        [Description(@"Get Subscription Quota limit.
        Input parameters:
        - subscriptionId: The subscription Id.
        - region: The region of the quota need to be retrieved.
        - quotaType: The quota type.
        The return value is a string containing the quota limit value for the specified subscription, region, and quota type.
        ")]
        public async Task<string> GetSubscriptionQuota(
            [Description("The subscription Id")] string subscriptionId,
            [Description("The region")] string region,
            [Description("The quota type")] string quotaType)
        {
            return await _plugin.GetSubscriptionQuota(subscriptionId, region, quotaType);
        }

        [KernelFunction(KernelFunctionNames.ACA.SetSubscriptionQuota)]
        [Description(@"Set Subscription Quota limit.
        Input parameters:
        - subscriptionId: The subscription Id.
        - region: The region of the quota need to be set.
        - quotaType: The quota type. 
        The return value is a boolean value for indicating if the operation is successful.
        ")]
        public async Task<string> SetSubscriptionQuota(
            [Description("The subscription Id")] string subscriptionId,
            [Description("The region")] string region,
            [Description("The quota type")] string quotaType,
            [Description("The target quota limit")] string quotaLimit)
        {
            return await _plugin.SetSubscriptionQuota(subscriptionId, region, quotaType, quotaLimit);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetContainerAppEnvironmentQuota)]
        [Description(@"Get Container App Environment Quota limit.
        Input parameters:
        - environmentResourceURL: The resource url of the container app environment.
        - region: The region of the quota need to be set.
        - quotaType: The quota type.
        The return value is a string containing the quota limit value for the specified environment, region, and quota type.
        ")]
        public async Task<string> GetEnvironmentQuota(
            [Description("The resource URL of the container app environment")] string environmentResourceURL,
            [Description("The region")] string region,
            [Description("The quota type")] string quotaType)
        {
            return await _plugin.GetEnvironmentQuota(environmentResourceURL, region, quotaType);
        }

        [KernelFunction(KernelFunctionNames.ACA.SetContainerAppEnvironmentQuota)]
        [Description(@"Set Managed Environment Quota limit.
Input parameters:
- incidentId: The incident id.
- environmentResourceURL: The resource url of the container app environment.
- region: The region of the quota need to be set.
- quotaType: The quota type.
- quotaLimit: The target quota limit.

Output:
- id: The trace id of te operation, which can be used to track the operation in the kusto table ContainerAppsAdminEvents.
- message: Describes the set Managed Environment Quota limit operation result.
")]
        public async Task<string> SetEnvironmentQuota(
    [Description("The incident id")] string incidentId,
    [Description("The resource URL of the container app environment")] string environmentResourceURL,
    [Description("The region")] string region,
    [Description("The quota type")] string quotaType,
    [Description("The target quota limit")] string quotaLimit)
        {
            return await _plugin.SetEnvironmentQuota(incidentId, environmentResourceURL, region, quotaType, quotaLimit);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetContainerAppEnvironmentQuotaOperationResult)]
        [Description(@"Get the operation result of setting Managed Environment Quota limit.
Input parameters:
- operationId: The trace id of the operation, which can be used to track the operation in the kusto table ContainerAppsAdminEvents.
- region: The region of the quota need to be set.

Output:
- PreciseTimeStamp: the time when the operation is completed.
- operationStatus: the status of the operation result.
- message: Describes the set Managed Environment Quota limit operation result.
")]
        public async Task<string> GetEnvironmentQuotaOperationResult(
            [Description("The operation id")] string operationId,
            [Description("The region")] string region)
        {
            return await _plugin.GetEnvironmentQuotaOperationResult(operationId, region);
        }

        [KernelFunction(KernelFunctionNames.ACA.ValidateQuotaRequest)]
        [Description(@"validate quota request
This function evaluates a quota request based on specified parameters, including quota type, region, target limit, and subscription id.
This operation determines whether the quota request adheres to approval rules and returns a validation result.

Input parameters:
- quotaType: (Required) The type of quota being requested.
- subscriptionId: (Required) The subscription ID associated with the quota request.
- region: (Required) The Azure region where the quota is requested.
- targetQuotaLimit: (Required) The target quota limit being requested.
- environmentResourceURL: (Optional) The resource url of the container app environment. It is optional for ManagedEnvironmentCount, SubscriptionNCA100Gpus, SubscriptionConsumptionNCA100Gpus, SubscriptionConsumptionT4Gpus, ContainerAppAdditionalPorts quota types. But Required for ManagedEnvironmentConsumptionCores, ManagedEnvironmentGeneralPurposeCores, ManagedEnvironmentMemoryOptimizedCores quota types.

Output:
The function returns a string containing three key pieces of information:

1. ApprovalResult: The status of the quota request, which can be one of the following:
   - Approved: The request has been successfully approved.
   - Rejected: The request has been denied.
   - Pending: Additional manual approval is required.
   - NotStarted: The request is incomplete and requires more details.
2. OfferType: The offer type of the subscription.
3. Reason: Provides an explanation for the validation decision.

This function helps ensure quota requests comply with predefined rules and provides a clear decision with supporting context.
")]
        public async Task<string> ValidateQuotaRequest(
            [Description("The quota type of the quota request")] string quotaType,
            [Description("The subscription id of the quota request")] string subscriptionId,
            [Description("The Azure region of the quota request")] string region,
            [Description("The target quota limit of the quota request")] string targetQuotaLimit,
            [Description("The managed environment resource uri")] string environmentResourceURL = "")
        {
            return await _plugin.ValidateQuotaRequest(quotaType, subscriptionId, region, targetQuotaLimit, environmentResourceURL);
        }
    }
}

