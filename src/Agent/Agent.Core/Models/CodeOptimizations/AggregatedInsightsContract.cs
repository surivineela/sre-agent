// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;
using Microsoft.OData.ModelBuilder.Core.V1;

namespace Agent.Core.Models
{
    /// <summary>
    /// Represents a contract for aggregated insights between the client and the DataPlane.
    /// </summary>
    public sealed class AggregatedInsightsContract
    {
        [JsonPropertyName("key")]
        public string Key { get; set; } = string.Empty;

        [JsonPropertyName("count")]
        public long Count { get; set; }

        [JsonPropertyName("appId")]
        public Guid AppId { get; set; } = Guid.Empty;

        [JsonPropertyName("issueId")]
        public string IssueId { get; set; } = string.Empty;

        [JsonPropertyName("criteria")]
        public double Criteria { get; set; }

        [JsonPropertyName("issueCategory")]
        public string IssueCategory { get; set; } = string.Empty;

        [JsonPropertyName("relation")]
        public string Relation { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public double Value { get; set; }

        [JsonPropertyName("provenance")]
        public string? Provenance { get; set; } = string.Empty;

        [JsonPropertyName("correlationId")]
        public string? CorrelationId { get; set; } = string.Empty;

        [JsonPropertyName("source")]
        public string? Source { get; set; }

        [JsonPropertyName("roleName")]
        public string? RoleName { get; set; } = string.Empty;

        [JsonPropertyName("containers")]
        public List<string> Containers { get; set; } = new();

        // Accepts ISO-8601 timestamps. System.Text.Json will parse from string automatically.
        [JsonPropertyName("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonPropertyName("context")]
        public List<string>? Context { get; set; }

        [JsonPropertyName("symbol")]
        public string Symbol { get; set; } = string.Empty;

        [JsonPropertyName("parentSymbol")]
        public string ParentSymbol { get; set; } = string.Empty;

        [JsonPropertyName("function")]
        public string Function { get; set; } = string.Empty;

        [JsonPropertyName("parentFunction")]
        public string ParentFunction { get; set; } = string.Empty;

        [JsonPropertyName("traceOccurrences")]
        public long TraceOccurrences { get; set; }

        [JsonPropertyName("isFixable")]
        public bool? IsFixable { get; set; }

        // StringMap<string> in TS -> Dictionary<string, string> in C#
        [JsonPropertyName("payload")]
        public Dictionary<string, string> Payload { get; set; } = new();
    }
}
