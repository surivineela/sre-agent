// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1
{
    public class StatusCount {
        public StatusCount(string Status, int Count) {
            this.Status = Status;
            this.Count = Count;
        }
        [JsonPropertyName("status")]
        public string Status { get; set; }
        [JsonPropertyName("count")]
        public int Count { get; set; }
    }

    public class IncidentThreadCounts
    {
        public IncidentThreadCounts(List<StatusCount> incidentStatusCounts, List<StatusCount> investigationStatusCounts)
        {
            this.IncidentStatusCounts = incidentStatusCounts;
            this.InvestigationStatusCounts = investigationStatusCounts;
        }
        [JsonPropertyName("incidentStatusCounts")]
        public List<StatusCount> IncidentStatusCounts { get; set; }
        [JsonPropertyName("investigationStatusCounts")]
        public List<StatusCount> InvestigationStatusCounts { get; set; }
    }
}
