// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json.Serialization;

namespace FirstPartyAgent.Models
{

    public class IcmIncident
    {
        public string? Id { get; set; }
        public string? Title { get; set; }
        public string? TeamsMessageId { get; set; }
    }

    public class QuotaIncidentState
    {
        public IcmIncident? Incident { get; set; }

        public string? Summary { get; set; }

        public DateTime? LastUpdateTimestamp { get; set; }

        public ApprovalState ApprovalResult { get; set; }

        public string? QuotaType { get; set; }

        public string? Region { get; set; }

        public string? SubscriptionId { get; set; }

        public int? TargetQuotaLimit { get; set; }

        public int? ApprovedQuotaLimit { get; set; }

        public string? OfferType { get; set; }

        public void UpdateFrom(QuotaIncidentState state)
        {
            if (state == null)
            {
                return;
            }

            LastUpdateTimestamp = DateTime.UtcNow;

            Summary = state.Summary;
            ApprovalResult = state.ApprovalResult;
            QuotaType = state.QuotaType;
            Region = state.Region;
            SubscriptionId = state.SubscriptionId;
            TargetQuotaLimit = state.TargetQuotaLimit;
            ApprovedQuotaLimit = state.ApprovedQuotaLimit;
            OfferType = state.OfferType;
        }

        public override string ToString()
        {
            StringBuilder sb = new StringBuilder();

            sb.AppendLine($"{Summary}<br/>");
            sb.AppendLine($"<br/>");
            sb.AppendLine($"-------- Following contains the extracted information so far --------<br/>");
            sb.AppendLine($"<br/>- ApprovalResult: {ApprovalResult}<br/>");

            if (!string.IsNullOrEmpty(QuotaType))
            {
                sb.AppendLine($"- QuotaType: {QuotaType}<br/>");
            }
            if (!string.IsNullOrEmpty(Region))
            {
                sb.AppendLine($"- Region: {Region}<br/>");
            }
            if (!string.IsNullOrEmpty(SubscriptionId))
            {
                sb.AppendLine($"- SubscriptionId: {SubscriptionId}<br/>");
            }
            if (TargetQuotaLimit.HasValue)
            {
                sb.AppendLine($"- TargetQuotaLimit: {TargetQuotaLimit}<br/>");
            }
            if (ApprovedQuotaLimit.HasValue)
            {
                sb.AppendLine($"- ApprovedQuotaLimit: {ApprovedQuotaLimit}<br/>");
            }
            if (!string.IsNullOrEmpty(OfferType))
            {
                sb.AppendLine($"- OfferType: {OfferType}<br/>");
            }
            return sb.ToString();
        }

        public string GetTestDescriber()
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine($"#ApprovalResult: {ApprovalResult}");
            sb.AppendLine($"#QuotaType: {QuotaType}");
            sb.AppendLine($"#Region: {Region}");
            sb.AppendLine($"#SubscriptionId: {SubscriptionId}");
            sb.AppendLine($"#TargetQuotaLimit: {TargetQuotaLimit}");
            sb.AppendLine($"#ApprovedQuotaLimit: {ApprovedQuotaLimit}");
            sb.AppendLine($"#OfferType: {OfferType}");
            return sb.ToString();
        }
    }

    public class ProcessQuotaIncidentRequest : IcmIncident
    {
        public string? Summary { get; set; }

        public IList<Discussion>? Discussions { get; set; }
    }

    public class Discussion
    {
        public Discussion(string user, DiscussionSource source, string message)
        {
            User = user;
            Source = source;
            Message = message;
        }

        public string User { get; set; }
        public DiscussionSource Source { get; set; }
        public string Message { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<DiscussionSource>))]
    public enum DiscussionSource
    {
        Teams,
        Icm,
    }

    [JsonConverter(typeof(JsonStringEnumConverter<ApprovalState>))]
    public enum ApprovalState
    {
        NotStarted,
        Pending,
        Approved,
        Rejected,
    }

    [JsonConverter(typeof(JsonStringEnumConverter<QuotaType>))]
    public enum QuotaType
    {
        /// <summary>
        /// Gpus quota for NCA100 workload profiles in subscription
        /// </summary>
        SubscriptionNCA100Gpus,

        /// <summary>
        /// Quota for consumption GPUs for NCA100 VMs per subscription
        /// </summary>
        SubscriptionConsumptionNCA100Gpus,

        /// <summary>
        /// Quota for consumption GPUs for T4 VMs per subscription
        /// </summary>
        SubscriptionConsumptionT4Gpus
    }
}