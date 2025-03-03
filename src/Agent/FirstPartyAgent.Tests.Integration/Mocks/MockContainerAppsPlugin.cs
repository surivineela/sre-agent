// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using FirstPartyAgent.Models;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Tests.End2End.Helpers;
using System.ComponentModel;

namespace FirstPartyAgent.Tests.Integration.Mocks
{
    public class MockContainerAppsPlugin : IContainerAppsPlugin
    {
        private readonly ContainerAppsPlugin containerAppsPlugin;

        public MockContainerAppsPlugin(ContainerAppsPlugin containerAppsPlugin)
        {
            this.containerAppsPlugin = containerAppsPlugin;
        }

        public async Task<SubscriptionDetail?> GetSubscriptionDetail(string subscriptionId)
        {
            var subscriptionInfoReaderHelper = new SubscriptionInfoReaderHelper();
            var offerInfo = subscriptionInfoReaderHelper.GetOfferTypeBySubscriptionId(subscriptionId);
            var quotaId = subscriptionInfoReaderHelper.GetQoutaIdBySubscriptionId(subscriptionId);

            var subscriptionDetail = new SubscriptionDetail();
            //(
            //    SubscriptionId: subscriptionId,
            //    BillingType: "",
            //    OfferType: offerInfo,
            //    OfferName: quotaId,
            //    TPId: 12345,
            //    BillableAcctId: "",
            //    CloudCustomerGuid: "",
            //    ClassifiedTypeV2: "",
            //    QuotaId: "",
            //    OrganizationName: ""
            //);

            return await Task.FromResult(subscriptionDetail);
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
            [Description("The offer type of the subscription")] string offerType,
            [Description("The region of the quota request")] string region,
            [Description("The target quota limit of the quota request")] string targetQuotaLimit
            )
        {
            return await containerAppsPlugin.ValidateQuotaRequest(quotaType, offerType, region, targetQuotaLimit);
        }
    }
}
