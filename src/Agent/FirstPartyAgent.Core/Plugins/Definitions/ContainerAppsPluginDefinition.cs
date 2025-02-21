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

The return value includes the detailed information of the given subscription id.
")]
        public async Task<SubscriptionDetail?> GetSubscriptionDetail(
            [Description("Subscription ID")] string subscriptionId)
        {
            return await _plugin.GetSubscriptionDetail(subscriptionId);
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
        public async Task<bool> SetSubscriptionQuota(
            [Description("The subscription Id")] string subscriptionId,
            [Description("The region")] string region,
            [Description("The quota type")] string quotaType)
        {
            return await _plugin.SetSubscriptionQuota(subscriptionId, region, quotaType);
        }
    }
}
