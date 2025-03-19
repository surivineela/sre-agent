// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using FirstPartyAgent.Models;
using System.ComponentModel;

namespace FirstPartyAgent.Plugins
{
    public interface IContainerAppsPlugin
    {
        public Task<SubscriptionDetail?> GetSubscriptionDetail(string subscriptionId);

        public Task<AcaSubscriptionUsage?> GetSubscriptionUsage(string subscriptionId);

        public Task<string> SetSubscriptionQuota(string subscriptionId, string region, string quotaType, string quotaLimit);

        public Task<TeamsPostMessageResponse?> PostTeamsDiscussionAsync(string incidentId, string title, string content);

        public Task<TeamsPostMessageResponse?> ReplyTeamsDiscussionAsync(string incidentId, string messageId, string content);

        public Task<string> ValidateQuotaRequest(string quotaType, string subscriptionId, string region, string targetQuotaLimit);
    }
}
