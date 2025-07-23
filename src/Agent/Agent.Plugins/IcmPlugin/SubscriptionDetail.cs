// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Plugins.IcmPlugin
{
    public sealed class SubscriptionDetail
    {
        public string? SubscriptionId { get; set; }
        public string? BillingType { get; set; }
        public string? OfferType { get; set; }
        public string? OfferName { get; set; }
        public int? TPId { get; set; }
        public string? BillableAcctId { get; set; }
        public string? CloudCustomerGuid { get; set; }
        public string? ClassifiedTypeV2 { get; set; }
        public string? QuotaId { get; set; }
        public string? OrganizationName { get; set; }
    }
}
