// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models;
using FirstPartyAgent.Models;
using FirstPartyAgent.Plugins;
using FirstPartyAgent.Tests.End2End.Helpers;

namespace FirstPartyAgent.Tests.Integration.Mocks
{
    public class MockContainerAppsPlugin : IContainerAppsPlugin
    {
        public async Task<SubscriptionDetail?> GetSubscriptionDetail(string subscriptionId)
        {
            var subscriptionInfoReaderHelper = new SubscriptionInfoReaderHelper();
            var offerInfo = subscriptionInfoReaderHelper.GetOfferTypeBySubscriptionId(subscriptionId);
            var quotaId = subscriptionInfoReaderHelper.GetQoutaIdBySubscriptionId(subscriptionId);

            var subscriptionDetail = new SubscriptionDetail
            (
                SubscriptionId: subscriptionId,
                BillingType: "MockBillingType",
                OfferType: offerInfo,
                OfferName: quotaId,
                TPId: 12345,
                BillableAcctId: "MockBillableAcctId",
                CloudCustomerGuid: "MockCloudCustomerGuid",
                ClassifiedTypeV2: "MockClassifiedTypeV2",
                QuotaId: "MockQuotaId",
                OrganizationName: "MockOrganizationName"
            );

            return await Task.FromResult(subscriptionDetail);
        }

        public async Task<bool> SetSubscriptionQuota(string subscriptionId, string region, string quotaType)
        {
            return await Task.FromResult(true);
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
    }
}
