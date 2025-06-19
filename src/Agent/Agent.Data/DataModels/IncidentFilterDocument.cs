// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

public record IncidentFilterDocument(
    string Id, // Filter Id
    string DocumentType,
    DateTime CreatedAt,
    string Name,
    string ImpactedService,
    string Priority,
    string IncidentType,
    string AlertId,
    string TitleContains,
    bool IsEnabled = true
) : ICosmosDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName; // Cosmos DB container name
    public string PartitionKey => DocumentType; // Use document type as partition key
    public bool IsDeleted { get; set; } = false; // Flag to indicate if the filter is deleted. This is used for soft delete.
    public DateTime UpdatedAt { get; set; } = CreatedAt;
    public string Name { get; set; } = Name;
    public string ImpactedService { get; set; } = ImpactedService;
    public string Priority { get; set; } = Priority;
    public string IncidentType { get; set; } = IncidentType;
    public string AlertId { get; set; } = AlertId;
    public string TitleContains { get; set; } = TitleContains;
    public bool IsEnabled { get; set; } = IsEnabled;
}


public class IncidentFilterDocumentPayload
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string ImpactedService { get; set; } = string.Empty;
    public string Priority { get; set; } = string.Empty;
    public string IncidentType { get; set; } = string.Empty;
    public string AlertId { get; set; } = string.Empty;
    public string TitleContains { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = true;
}
