// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Agent.Core.Models.ServiceNow
{
    /// <summary>
    /// Represents a ServiceNow webhook payload
    /// </summary>
    public class ServiceNowWebhookPayload
    {
        [JsonProperty("incident")]
        public ServiceNowWebhookIncident Incident { get; set; }
        
        [JsonProperty("action")]
        public string? Action { get; set; }
        
        [JsonProperty("webhook_id")]
        public string? WebhookId { get; set; }
        
        [JsonProperty("timestamp")]
        public DateTime? Timestamp { get; set; }
    }
    
    public class ServiceNowWebhookIncident
    {
        [JsonProperty("sys_id")]
        [Required]
        public string IncidentId { get; set; }
        
        [JsonProperty("number")]
        public string? Number { get; set; }
        
        [JsonProperty("short_description")]
        public string? Title { get; set; }
        
        [JsonProperty("description")]
        public string? Description { get; set; }
        
        [JsonProperty("priority")]
        public string? Priority { get; set; }
        
        [JsonProperty("state")]
        public string? State { get; set; }
        
        [JsonProperty("assigned_to")]
        public string? AssignedTo { get; set; }
        
        [JsonProperty("sys_created_on")]
        public DateTime? CreatedOn { get; set; }
        
        [JsonProperty("sys_updated_on")]
        public DateTime? UpdatedOn { get; set; }
        
        [JsonProperty("comments")]
        public string? Comments { get; set; }
        
        [JsonProperty("work_notes")]
        public string? WorkNotes { get; set; }
        
        [JsonProperty("cmdb_ci")]
        public string? ConfigurationItem { get; set; }
        
        [JsonProperty("additional_properties")]
        public Dictionary<string, string>? AdditionalProperties { get; set; }
    }
}
