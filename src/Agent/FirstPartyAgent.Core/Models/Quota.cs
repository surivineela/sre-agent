// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text;
using System.Text.Json.Serialization;

namespace FirstPartyAgent.Models
{
    public class QuotaIncidentState
    {
        public Incident? Incident { get; set; }

        public ConversationContext ConversationContext { get; set; } = new ConversationContext();

        public string? Summary { get; set; }

        public DateTime? LastUpdateTimestamp { get; set; }

        public ApprovalState ApprovalResult { get; set; }

        public string? QuotaType { get; set; }

        public string? Region { get; set; }

        public string? SubscriptionId { get; set; }

        public int? TargetQuotaLimit { get; set; }

        public int? ApprovedQuotaLimit { get; set; }

        public string? OfferType { get; set; }

        public bool IsNewRequest { get; set; } = true;

        public bool HasBeenPended { get; set; } = false;

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
            StringBuilder stateBuilder = new StringBuilder();

            if (!string.IsNullOrEmpty(QuotaType))
            {
                stateBuilder.AppendLine($"- QuotaType: {QuotaType}<br/>");
            }
            if (!string.IsNullOrEmpty(Region))
            {
                stateBuilder.AppendLine($"- Region: {Region}<br/>");
            }
            if (!string.IsNullOrEmpty(SubscriptionId))
            {
                stateBuilder.AppendLine($"- SubscriptionId: {SubscriptionId}<br/>");
            }
            if (TargetQuotaLimit.HasValue)
            {
                stateBuilder.AppendLine($"- TargetQuotaLimit: {TargetQuotaLimit}<br/>");
            }
            if (ApprovedQuotaLimit.HasValue)
            {
                stateBuilder.AppendLine($"- ApprovedQuotaLimit: {ApprovedQuotaLimit}<br/>");
            }
            if (!string.IsNullOrEmpty(OfferType))
            {
                stateBuilder.AppendLine($"- OfferType: {OfferType}<br/>");
            }

            StringBuilder messageBuilder = new StringBuilder();
            messageBuilder.AppendLine($"{Summary}<br/>");

            if (!string.IsNullOrWhiteSpace(stateBuilder.ToString()))
            {
                messageBuilder.AppendLine($"<br/>");
                messageBuilder.AppendLine($"-------- Following contains the extracted information so far --------<br/>");
                messageBuilder.AppendLine(stateBuilder.ToString());
            }

            return messageBuilder.ToString();
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

    public class ConversationContext
    {
        public string TeamsMessageId { get; set; } = string.Empty;

        public string IncidentId { get; set; } = string.Empty;
    }

    public class ConversationEntry
    {
        public ConversationEntry(string user, ConversationSource source, string message)
        {
            User = user;
            Source = source;
            Message = message;
        }

        public string User { get; set; }
        public ConversationSource Source { get; set; }
        public string Message { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter<ConversationSource>))]
    public enum ConversationSource
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
        NotSupported,
    }
}
