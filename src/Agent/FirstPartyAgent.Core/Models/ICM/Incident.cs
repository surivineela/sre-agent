// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Newtonsoft.Json;

namespace FirstPartyAgent.Models
{
    public class Incident
    {
        public string IncidentId { get; set; }
        public IncidentType IncidentType { get; set; }
        public string CloudInstance { get; set; }
        public string Slice { get; set; }
        public int HitCount { get; set; }
        public string ParentIncidentId { get; set; } = string.Empty;
        public string Environment { get; set; } = string.Empty;
        public string CreatedBy { get; set; }
        public DateTime ImpactStartDate { get; set; }
        public DateTime CreatedDate { get; set; }
        public DateTime ModifiedDate { get; set; }
        public IncidentStatus Status { get; set; }
        public string OwningService { get; set; }
        public string OwningServiceId { get; set; }
        public string OwningTeam { get; set; }
        public string OwningTeamName { get; set; }
        public string Owner { get; set; }
        public string Severity { get; set; }
        public string Title { get; set; }
        public string Summary { get; set; }
        public string DiscussionEntry { get; set; }
        public string MonitoringRole { get; set; }
        public string MonitoringSlice { get; set; }
        public string[] Tags { get; set; } = Array.Empty<string>();
    }

    public class DiscussionEntry
    {
        public string IncidentId { get; set; }
        public DateTime Date { get; set; }
        public string ChangedBy { get; set; }
        public string Text { get; set; }
        public bool IsHtml { get; set; }
        public string? Cause { get; set; }
    }

    public class CustomField
    {
        public string CustomFieldName { get; set; }
        public string CustomFieldValue { get; set; }
    }

    public class SearchItem
    {
        public string Id { get; set; }
        public string Title { get; set; }
        public string ResponsibleServiceName { get; set; }
        public DateTime CreatedDate { get; set; }
        public string HowFixed { get; set; }
        public string State { get; set; }
    }


    public class ODataResponse<T>
    {
        [JsonProperty("odata.metadata")]
        public string OdataMetadata { get; set; }

        [JsonProperty("value")]
        public List<T> Value { get; set; }
    }
}

