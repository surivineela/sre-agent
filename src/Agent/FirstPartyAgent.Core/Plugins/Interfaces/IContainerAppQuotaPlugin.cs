// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using FirstPartyAgent.Models;

namespace FirstPartyAgent.Plugins
{
    public interface IContainerAppQuotaPlugin
    {
        public Task<string> SetSubscriptionQuota(string subscriptionId, string region, string quotaType, string quotaLimit);

        public Task<string> ValidateQuotaRequest(string quotaType, string subscriptionId, string region, string targetQuotaLimit);
    }
}

