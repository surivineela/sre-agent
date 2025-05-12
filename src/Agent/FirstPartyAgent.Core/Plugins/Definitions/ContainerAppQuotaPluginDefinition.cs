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

        [KernelFunction(KernelFunctionNames.ACA.ValidateQuotaRequest)]
        [Description(@"validate quota request
This function evaluates a quota request based on specified parameters, including quota type, region, target limit, and subscription id.
This operation determines whether the quota request adheres to approval rules and returns a validation result.

Output:
The function returns a string containing two key pieces of information:

1. ApprovalResult: The status of the quota request, which can be one of the following:
   - Approved: The request has been successfully approved.
   - Rejected: The request has been denied.
   - Pending: Additional manual approval is required.
   - NotStarted: The request is incomplete and requires more details.
2. OfferType: The offer type of the subscription.
2. Reason: Provides an explanation for the validation decision.

This function helps ensure quota requests comply with predefined rules and provides a clear decision with supporting context.
")]
        public async Task<string> ValidateQuotaRequest(
            [Description("The quota type of the quota request")] string quotaType,
            [Description("The subscription id of the quota request")] string subscriptionId,
            [Description("The Azure region of the quota request")] string region,
            [Description("The target quota limit of the quota request")] string targetQuotaLimit)
        {
            return await _plugin.ValidateQuotaRequest(quotaType, subscriptionId, region, targetQuotaLimit);
        }
    }
}

