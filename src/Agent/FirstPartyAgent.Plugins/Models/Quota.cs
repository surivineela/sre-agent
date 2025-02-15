using System.Text.Json.Serialization;

namespace FirstPartyAgent.Models
{
    public record QuotaIncidentState
    {
        public string IncidentId { get; set; }

        public string? Title { get; set; }

        public string? TeamsMessageId { get; set; }

        public string Summary { get; set; }

        public QuotaRequest? Request { get; set; }

        public DateTime? LastUpdateTimestamp { get; set; }

        public string SummarizeState()
        {
            return $"""
                {Summary}
                -------- Following contains the extracted information so far --------
                {Request?.ToString()}
                """;
        }
    }

    public record ProcessQuotaIncidentRequest : QuotaIncidentState
    {
        public IList<Disscussion>? Discussions { get; set; }
    }

    public record Disscussion
    {
        public Disscussion(string user, DiscussionSource source, string message)
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

    public record QuotaRequest
    {
        public ApprovalState ApprovalResult { get; set; }
        public string Message { get; set; }
        public string? QuotaType { get; set; }
        public string? Region { get; set; }
        public string? SubscriptionId { get; set; }
        //public string? ResourceId { get; set; }
        public int? TargetQuotaLimit { get; set; }
        public int? ApprovedQuotaLimit { get; set; }
        public string? OfferType { get; set; }

        public override string ToString()
        {
            var str = $"<br/>- ApprovalResult: {ApprovalResult}<br/>";
            if (!string.IsNullOrEmpty(QuotaType))
            {
                str += $"- QuotaType: {QuotaType}<br/>";
            }
            if (!string.IsNullOrEmpty(Region))
            {
                str += $"- Region: {Region}<br/>";
            }
            if (!string.IsNullOrEmpty(SubscriptionId))
            {
                str += $"- SubscriptionId: {SubscriptionId}<br/>";
            }
            if (TargetQuotaLimit.HasValue)
            {
                str += $"- TargetQuotaLimit: {TargetQuotaLimit}<br/>";
            }
            if (ApprovedQuotaLimit.HasValue)
            {
                str += $"- ApprovedQuotaLimit: {ApprovedQuotaLimit}<br/>";
            }
            if (!string.IsNullOrEmpty(OfferType))
            {
                str += $"- OfferType: {OfferType}<br/>";
            }
            return str;
        }
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