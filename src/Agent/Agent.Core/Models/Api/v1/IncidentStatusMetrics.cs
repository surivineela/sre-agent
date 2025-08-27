// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Core.Models.Api.v1
{
    public class IncidentStatusMetrics
    {
        public int ActiveCount { get; set; }
        public int MitigatedCount { get; set; }
        public int ResolvedCount { get; set; }
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum PagerDutyIncidentStatus
    {
        Triggered,
        Acknowledged,
        Resolved
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum AzMonIncidentStatus
    {
        New,
        Acknowledged,
        Closed
    }
}
