// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

namespace Agent.Data.DataModels;

public record IncidentHandlerDocument(
    string Id, // Handler Id
    string DocumentType, // Type of the document, e.g. "IncidentHandler"
    string Name, // A user friendly name for the handler
    string Description, // A description of the handler, e.g. "This handler is used to process auth related incidents."
    string IncidentFilterId, // For filtering incidents where the handler applies
    List<string> IncidentProcessingGuide, // Guide that should be followed by the handler to act on incidents
    List<string> Tools, // List of tools that the handler can use to process incidents.
    List<string> Incidents, // List of incident IDs whose learnings were used to generate the IncidentProcessingGuide.
    string CustomInstructions, // Custom instructions provided by human engineers that were used to generate the IncidentProcessingGuide.
    DateTime CreatedAt
) : ICosmosDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName; // Cosmos DB container name
    public string PartitionKey => DocumentType; // Use document type as partition key
    public bool IsDeleted { get; set; } = false; // Flag to indicate if the handler is deleted. This is used for soft delete.
    public DateTime UpdatedAt { get; set; } = CreatedAt;
    public string Name { get; set; } = Name; // Name of the handler, can be used for display purposes.
    public string Description { get; set; } = Description; // Description of the handler, can be used for display purposes.
    public string IncidentFilterId { get; set; } = IncidentFilterId; // To filter incidents where the handler applies.
    public List<string> IncidentProcessingGuide { get; set; } = IncidentProcessingGuide; // Guide that should be followed by the handler to act on incidents.
    public List<string> Tools { get; set; } = Tools; // List of tools that the handler can use to process incidents.
    public List<string> Incidents { get; set; } = Incidents; // List of incident IDs whose learnings were used to generate the IncidentProcessingGuide.
    public string CustomInstructions { get; set; } = CustomInstructions; // Custom instructions provided by human engineers that were used to generate the IncidentProcessingGuide.
}

public class IncidentHandlerDocumentPayload
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string IncidentFilterId { get; set; } = string.Empty;
    public List<string> IncidentProcessingGuide { get; set; } = [];
    public List<string> Tools { get; set; } = [];
    public List<string> Incidents { get; set; } = [];
    public string CustomInstructions { get; set; } = string.Empty;
}
