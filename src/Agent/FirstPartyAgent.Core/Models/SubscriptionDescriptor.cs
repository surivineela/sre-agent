// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;

namespace FirstPartyAgent.Models
{
    [Description("The internal details of a subcription")]
    public sealed record SubscriptionDetail(
        string SubscriptionId,
        string BillingType = "",
        string OfferType = "",
        string OfferName = "",
        int? TPId = null,
        string BillableAcctId = "",
        string CloudCustomerGuid = "",
        string ClassifiedTypeV2 = "",
        string QuotaId = "",
        string OrganizationName = "");
}
