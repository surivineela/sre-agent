// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

/// <summary>
/// ServiceNow-specific incident indexing configuration document.
/// Stored in CosmosDB with provider-discriminated ID.
/// </summary>
public class ServiceNowIncidentIndexingConfigurationDocument : IIncidentIndexingConfigurationDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public string Id { get; set; } = IncidentIndexingConfigurationUtilities.GetDocumentId(IncidentManagementType.ServiceNow);

    public string DocumentType => IncidentIndexingConfigurationUtilities.GetDocumentTypeName(IncidentManagementType.ServiceNow);

    public string PartitionKey => DocumentType;

    public IncidentManagementType ProviderType => IncidentManagementType.ServiceNow;

    // ServiceNow-specific fields (to be defined when implementing full support)
    public string AssignmentGroup { get; set; } = string.Empty;
    public List<string> Categories { get; set; } = new();
    public List<int> Priorities { get; set; } = new() { 1, 2, 3 };

    // Common fields from interface
    public int LookbackDays { get; set; } = 90;
    public int? MaxIncidents { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// ServiceNow-specific payload for configuration requests.
/// </summary>
public record ServiceNowIncidentIndexingConfigurationPayload : IncidentIndexingConfigurationPayload
{
    public string AssignmentGroup { get; init; } = string.Empty;
    public List<string> Categories { get; init; } = new();
    public List<int> Priorities { get; init; } = new() { 1, 2, 3 };
}
