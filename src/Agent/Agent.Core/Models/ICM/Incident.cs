namespace Agent.Core.Models.ICM
{
    public class Incident
    {
        public string IncidentId { get; set; }
        public string? Severity { get; set; }
        public string? Title { get; set; }
        public string Summary { get; set; }
        public string? Status { get; set; }
        public string? DiscussionEntry { get; set; }
    }

    public class DiscussionEntry
    {
        public string IncidentId { get; set; }
        public DateTime Date { get; set; }
        public string ChangedBy { get; set; }
        public string Text { get; set; }
        public bool IsHtml { get; set; }
    }
}
