// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using FirstPartyAgent.Constants;
using FirstPartyAgent.Models;
using Microsoft.SemanticKernel;

namespace FirstPartyAgent.Plugins.Definitions
{
    /// <summary>
    /// Using this approach because SK does not allow interfaces to be used as kernel functions
    /// https://github.com/microsoft/semantic-kernel/issues/10323
    /// </summary>
    /// <param name="plugin"></param>
    public class ContainerAppsPluginDefinition(IContainerAppsPlugin plugin)
    {
        private readonly IContainerAppsPlugin _plugin = plugin;

        [KernelFunction(KernelFunctionNames.ACA.GetSubscriptionDetail)]
        [Description(@"Get User subscription detail
This operation will get the detailed information of the given Azure subscription id.

Input parameters:
- subscriptionId: The Id of the Azure subscription

Output:
The return value includes the detailed information of the given subscription id with the following information: SubscriptionId,BillingType, OfferType, OfferName, TPId, BillableAcctId, CloudCustomerGuid, ClassifiedTypeV2, QuotaId, OrganizationName 
")]
        public async Task<SubscriptionDetail?> GetSubscriptionDetail(
            [Description("Subscription ID")] string subscriptionId)
        {
            return await _plugin.GetSubscriptionDetail(subscriptionId);
        }

        [KernelFunction(KernelFunctionNames.ACA.GetSubscriptionUsage)]
        [Description(@"Get the subscription usage information in Container Apps Service.
Input parameters:
- subscriptionId: The Id of the Azure subscription

Output:
The return value includes the subscription usage information in Container Apps Service of the given subscription id.
- SubscriptionId: The Id of the Azure subscription
- NumberOfEnvironments: The number of environments created in the given subscription id.
- NumberOfContainerApps: The number of container apps created in the given subscription id.
- NumberOfJobs: The number of jobs created in the given subscription id.
- TrustLevel: The trust level of the subscription
")]
        public async Task<AcaSubscriptionUsage?> GetSubscriptionUsage(
            [Description("Subscription ID")] string subscriptionId)
        {
            return await _plugin.GetSubscriptionUsage(subscriptionId);
        }
    }
}
