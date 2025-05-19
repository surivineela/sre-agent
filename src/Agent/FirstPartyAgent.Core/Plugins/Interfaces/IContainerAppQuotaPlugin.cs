// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Plugins
{
    public interface IContainerAppQuotaPlugin
    {
        public Task<string> SetSubscriptionQuota(string subscriptionId, string region, string quotaType, string quotaLimit);

        public Task<string> SetEnvironmentQuota(string incidentId, string managedEnvironmentResourceUri, string region, string quotaType, string quotaLimit);

        public Task<string> ValidateQuotaRequest(string quotaType, string subscriptionId, string region, string targetQuotaLimit, string environmentResourceURL);

        public Task<string> GetEnvironmentQuotaOperationResult(string operationId, string region);

    }
}

