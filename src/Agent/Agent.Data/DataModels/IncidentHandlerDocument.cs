// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel.DataAnnotations;
using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

public class IncidentHandlerDocument : IncidentHandlerDocumentPayload, ICosmosDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName; // Cosmos DB container name

    public required string DocumentType { get; init; }
    public string PartitionKey => DocumentType; // Use document type as partition key

    public bool IsDeleted { get; set; } = false; // Flag to indicate if the handler is deleted. This is used for soft delete.

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public static string GetDocumentType(IncidentManagementType? type)
    {
        return $"IncidentHandler{type.ToString()}";
    }
}

public class IncidentHandlerDocumentPayload
{
    public string Id { get; init; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IncidentFilterId { get; set; } = string.Empty;
    public List<string> IncidentProcessingGuide { get; set; } = [];
    public List<string> Tools { get; set; } = [];
    public List<string> Incidents { get; set; } = [];
    public string CustomInstructions { get; set; } = string.Empty;
}
