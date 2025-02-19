// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using FirstPartyAgent.Models;

namespace FirstPartyAgent.Plugins
{
    public interface IContainerAppsPlugin
    {
        public Task<SubscriptionDetail?> GetSubscriptionDetail(string subscriptionId);

        public Task<bool> SetSubscriptionQuota(string subscriptionId, string region, string quotaType);

        public Task<TeamsPostMessageResponse?> PostTeamsDiscussionAsync(string incidentId, string title, string content);

        public Task<TeamsPostMessageResponse?> ReplyTeamsDiscussionAsync(string incidentId, string messageId, string content);
    }
}
