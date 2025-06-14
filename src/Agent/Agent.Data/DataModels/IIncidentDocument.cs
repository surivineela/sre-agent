// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

public interface IIncidentDocument : ICosmosDocument
{
    DateTime CreatedAt { get; }
    DateTime UpdatedAt { get; set; }
    string ImpactedServiceId { get; set; }
    string ImpactedServiceName { get; set; }
    string Id { get; }
    string Status { get; set; }
    string IncidentType { get; set; }
    string Priority { get; set; }
    string Severity { get; set; }
    string Title { get; set; }
    string Description { get; set; }
    string ExtractedKnowledge { get; set; }
}

// Create a record type that implements the interface called CommonIncidentDocument
public record CommonIncidentDocument(
    string Id,
    string Status,
    string Priority,
    string IncidentType,
    string ImpactedServiceId,
    string ImpactedServiceName,
    DateTime CreatedAt,
    string DocumentType
) : IIncidentDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;
    public string DocumentType { get; } = DocumentType;
    public string Id { get; } = Id; // Use the incident id as the document id
    public string PartitionKey => Id; // Use incident id as partition key
    public DateTime CreatedAt { get; } = CreatedAt;
    public DateTime UpdatedAt { get; set; }
    public string ImpactedServiceId { get; set; } = ImpactedServiceId;
    public string ImpactedServiceName { get; set; } = ImpactedServiceName;
    public string Status { get; set; } = Status;
    public string IncidentType { get; set; } = IncidentType;
    public string Priority { get; set; } = Priority;
    public string Severity { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ExtractedKnowledge { get; set; } = string.Empty;
}
