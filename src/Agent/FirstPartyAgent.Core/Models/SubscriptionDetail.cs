// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace FirstPartyAgent.Models
{
    public sealed class SubscriptionDetail
    {
        public string SubscriptionId { get; set; } = string.Empty;
        public string BillingType { get; set; } = string.Empty;
        public string OfferType { get; set; } = string.Empty;
        public string OfferName { get; set; } = string.Empty;
        public int? TPId { get; set; } 
        public string BillableAcctId { get; set; } = string.Empty;
        public string CloudCustomerGuid { get; set; } = string.Empty;
        public string ClassifiedTypeV2 { get; set; } = string.Empty;
        public string QuotaId { get; set; } = string.Empty;
        public string OrganizationName { get; set; } = string.Empty;
    }
}
