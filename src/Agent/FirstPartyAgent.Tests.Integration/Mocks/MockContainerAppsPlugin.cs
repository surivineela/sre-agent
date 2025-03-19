// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using FirstPartyAgent.Models;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Tests.End2End.Helpers;
using Newtonsoft.Json;
using System.ComponentModel;
using static FirstPartyAgent.Plugins.ContainerAppsPlugin;

namespace FirstPartyAgent.Tests.Integration.Mocks
{
    public class MockContainerAppsPlugin : IContainerAppsPlugin
    {
        public async Task<SubscriptionDetail?> GetSubscriptionDetail(string subscriptionId)
        {
            var subscriptionInfoReaderHelper = new SubscriptionInfoReaderHelper();
            var offerType = subscriptionInfoReaderHelper.GetOfferTypeBySubscriptionId(subscriptionId);
            var quotaId = subscriptionInfoReaderHelper.GetQoutaIdBySubscriptionId(subscriptionId);

            var subscriptionDetail = new SubscriptionDetail();
            subscriptionDetail.OfferType = offerType;

            return await Task.FromResult(subscriptionDetail);
        }

        public async Task<AcaSubscriptionUsage?> GetSubscriptionUsage(string subscriptionId)
        {
            var subscriptionUsage = new AcaSubscriptionUsage();
            return await Task.FromResult(subscriptionUsage);
        }

        public async Task<string> SetSubscriptionQuota(string subscriptionId, string region, string quotaType, string quotaLimit)
        {
            return await Task.FromResult(string.Empty);
        }

        public async Task<TeamsPostMessageResponse?> PostTeamsDiscussionAsync(string incidentId, string title, string content)
        {
            return await Task.FromResult(new TeamsPostMessageResponse
            {
                MessageId = "MockMessageId"
            });
        }

        public async Task<TeamsPostMessageResponse?> ReplyTeamsDiscussionAsync(string incidentId, string messageId, string content)
        {
            return await Task.FromResult(new TeamsPostMessageResponse
            {
                MessageId = "MockReplyMessageId"
            });
        }

        public async Task<string> ValidateQuotaRequest(
            [Description("The quota type of the quota request")] string quotaType,
            [Description("The subscription id of the quota request")] string subscriptionId,
            [Description("The region of the quota request")] string region,
            [Description("The target quota limit of the quota request")] string targetQuotaLimit
            )
        {
            var mockSubscriptionDetails = await this.GetSubscriptionDetail(subscriptionId);
            var offerType = mockSubscriptionDetails?.OfferType;
            if (string.IsNullOrEmpty(offerType))
            {
                return JsonConvert.SerializeObject(new
                {
                    ApproveResult = ApprovalState.NotStarted.ToString(),
                    OfferType = "Unknown",
                    Reason = string.Format(MessageTemplates.SubscriptionInformationMissing, "offer type")
                });
            }
            var validationResult = ContainerAppsPlugin.ValidateQuotaRule(targetQuotaLimit, quotaType, region.ToLowerInvariant(), offerType);
            var approvalResult = validationResult.approvalState.ToString();

            string result = JsonConvert.SerializeObject(new
            {
                ApproveResult = approvalResult,
                OfferType = offerType,
                Reason = validationResult.reason
            });

            return result;
        }
    }
}
