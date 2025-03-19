// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Models;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Octokit;

namespace FirstPartyAgent.Plugins.Definitions
{
    /// <summary>
    /// Using this approach because SK does not allow interfaces to be used as kernel functions
    /// https://github.com/microsoft/semantic-kernel/issues/10323
    /// </summary>
    /// <param name="plugin"></param>
    public class ContainerAppsPluginDefinition(IContainerAppsPlugin plugin, ILogger<ContainerAppsPluginDefinition> logger)
    {
        private readonly IContainerAppsPlugin _plugin = plugin;
        private readonly ILogger<ContainerAppsPluginDefinition> _logger = logger;


        [KernelFunction(KernelFunctionNames.ACA.GetSubscriptionDetail)]
        [Description(@"Get User subscription detail
This operation will get the detailed information of the given Azure subscription id.

Input parameters:
- subscriptionId: The Id of the Azure subscription

The return value includes the detailed information of the given subscription id.
")]
        public async Task<SubscriptionDetail?> GetSubscriptionDetail(
            [Description("Subscription ID")] string subscriptionId)
        {
            _logger.LogInformation($"GetSubscriptionDetail Started: {subscriptionId}");
            var result = await _plugin.GetSubscriptionDetail(subscriptionId);
            _logger.LogInformation($"GetSubscriptionDetail Completed: {result?.OfferType}, {result?.QuotaId}");
            return result;
        }

        //[KernelFunction(KernelFunctionNames.ACA.SetSubscriptionQuota)]
        //[Description(@"Call GenevaAction SetSubscriptionQuota
        //The GenevaAction is a name of DevOps tool, it contains a series of operation call be invoke for different purpose.
        //This operation 'SetSubscriptionQuota' triggers the GenevaAction workflow to set the quota for a subscription.

        //Input parameters:
        //- subscriptionId: The subscription Id
        //- region: The region of the quota need to be set.
        //- quotaType: The quota type. Allowed value are:
        //    SubscriptionNCA100Gpus - The NCA100 GPU quota for dedicated workload
        //    SubscriptionConsumptionNCA100Gpus - The NCA100 GPU quota for consumption workload
        //    SubscriptionConsumptionT4Gpus - The T4 GPU quota for consumption workload

        //The operation will update the given quota type for given subscription in the given region.
        //The return value is a boolean value for indicating if the operation is successful.
        //")]
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
            _logger.LogInformation($"ValidateQuotaRequest Started: {quotaType}, {subscriptionId}, {region}, {targetQuotaLimit}");
            var message = await _plugin.ValidateQuotaRequest(quotaType, subscriptionId, region, targetQuotaLimit);
            _logger.LogInformation($"ValidateQuotaRequest Completed: {message}");
            return message;
        }
    }
}
