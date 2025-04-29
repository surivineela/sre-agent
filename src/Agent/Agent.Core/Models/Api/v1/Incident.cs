namespace Agent.Core.Models.Api.v1
{
    /// <summary>
    /// Represents a PagerDuty incident.
    /// </summary>
    public record PagerDutyIncident(
        string Id, // Incident ID
        string HtmlUrl,
        string Status, // Incident status: triggered, acknowledged, resolved
        DateTime CreatedAt
    )
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }

    /// <summary>
    /// Represents an Azure Monitor alert.
    /// </summary>
    public record AzMonitorAlert(
        string Id,
        string Name,
        string Severity,
        string TargetResourceType,
        string TargetResourceId,
        string SubscriptionId,
        string Status, // Alert status: New, Acknowledged, Closed
        DateTime CreatedAt
    )
    {
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime UpdatedAt { get; set; }
    }
}
