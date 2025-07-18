// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Models.Api.v1;
using Agent.Core.Models.ICM;
using Agent.Core.Models.ServiceNow;
using System;
using System.Collections.Generic;

namespace Agent.Data.DataModels
{
    public record ServiceNowIncidentDocument(
        string Id, // Incident ID
        string Number, 
        string Status, // Incident status: new, in-progress, resolved
        string Priority, // e.g. 1, 2, 3, 4, 5
        string Urgency, // e.g. high, medium, low
        string IncidentType, 
        string ImpactedServiceId, 
        string ImpactedServiceName,
        DateTime CreatedAt
    ) : IIncidentDocument
    {
        public static string ContainerName => AgentDataConfiguration.ThreadContainerName;
        public string DocumentType { get; } = "ServiceNowIncident";
        public string Id { get; init; } = Id; // Use the incident id as the document id
        public string PartitionKey => Id;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public List<DiscussionEntry> DiscussionEntries { get; set; } = new();
        public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
        public string AssignedTo { get; set; } = string.Empty;
        public string Status { get; set; } = Status;
        public string IncidentType { get; set; } = IncidentType;
        public string Priority { get; set; } = Priority;
        public string ImpactedServiceId { get; set; } = ImpactedServiceId;
        public string ImpactedServiceName { get; set; } = ImpactedServiceName;
        public string Severity { get; set; } = string.Empty;
        public string ExtractedKnowledge { get; set; } = string.Empty;
        public string IncidentSystemId { get; set; } = string.Empty;

        public ServiceNowIncidentDocument() : this(
            string.Empty, 
            string.Empty, 
            string.Empty, 
            string.Empty, 
            string.Empty, 
            "ServiceNow", 
            string.Empty, 
            string.Empty, 
            DateTime.UtcNow)
        {
        }

        public ServiceNowIncidentDocument(ServiceNowIncident incident) : this(
            incident.Number,  // Use Number as the document Id instead of IncidentId
            incident.Number,
            incident.State,
            incident.Priority,
            incident.Urgency,
            "ServiceNow",
            incident.ImpactedServiceId,
            incident.ImpactedServiceName,
            incident.CreatedAt)
        {
            Title = incident.Title;
            Description = incident.Description;
            UpdatedAt = incident.UpdatedAt;
            AssignedTo = incident.AssignedTo;
            IncidentSystemId = incident.IncidentId;  // Store the original sys_id
        }
    }
}
