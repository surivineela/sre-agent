using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace Agent.Core.Models.ServiceNow
{
    public class ServiceNowResponse<T>
    {
        [JsonPropertyName("result")]
        public T? Result { get; set; }
    }

    public class ServiceNowListResponse<T>
    {
        [JsonPropertyName("result")]
        public List<T>? Result { get; set; } = new();
    }

    public class ServiceNowIncident
    {
        [JsonPropertyName("sys_id")]
        public string IncidentId { get; set; } = string.Empty;

        [JsonPropertyName("number")]
        public string Number { get; set; } = string.Empty;

        [JsonPropertyName("short_description")]
        public string Title { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string Description { get; set; } = string.Empty;

        [JsonPropertyName("state")]
        public string State { get; set; } = string.Empty;

        [JsonPropertyName("priority")]
        public string Priority { get; set; } = string.Empty;

        [JsonPropertyName("urgency")]
        public string Urgency { get; set; } = string.Empty;

        [JsonPropertyName("cmdb_ci")]
        [JsonConverter(typeof(ServiceNowStringConverter))]
        public string ImpactedServiceId { get; set; } = string.Empty;

        [JsonPropertyName("cmdb_ci_name")]
        public string ImpactedServiceName { get; set; } = string.Empty;

        [JsonPropertyName("assigned_to")]
        [JsonConverter(typeof(ServiceNowStringConverter))]
        public string AssignedTo { get; set; } = string.Empty;

        [JsonPropertyName("sys_created_on")]
        [JsonConverter(typeof(ServiceNowDateTimeConverter))]
        public DateTime CreatedAt { get; set; }

        [JsonPropertyName("sys_updated_on")]
        [JsonConverter(typeof(ServiceNowDateTimeConverter))]
        public DateTime UpdatedAt { get; set; }
    }

    public class ServiceNowDiscussionEntry
    {
        [JsonPropertyName("sys_id")]
        public string Id { get; set; } = string.Empty;

        [JsonPropertyName("element_id")]
        public string IncidentId { get; set; } = string.Empty;

        [JsonPropertyName("value")]
        public string Text { get; set; } = string.Empty;

        [JsonPropertyName("sys_created_by")]
        public string ChangedBy { get; set; } = string.Empty;

        [JsonPropertyName("sys_created_on")]
        [JsonConverter(typeof(ServiceNowDateTimeConverter))]
        public DateTime Date { get; set; }
    }
}
