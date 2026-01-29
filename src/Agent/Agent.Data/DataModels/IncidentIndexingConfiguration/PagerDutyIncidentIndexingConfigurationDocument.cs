// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

/// <summary>
/// PagerDuty-specific incident indexing configuration document.
/// Stored in CosmosDB with provider-discriminated ID.
/// </summary>
public class PagerDutyIncidentIndexingConfigurationDocument : IIncidentIndexingConfigurationDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public string Id { get; set; } = IncidentIndexingConfigurationUtilities.GetDocumentId(IncidentManagementType.PagerDuty);

    public string DocumentType => IncidentIndexingConfigurationUtilities.GetDocumentTypeName(IncidentManagementType.PagerDuty);

    public string PartitionKey => DocumentType;

    public IncidentManagementType ProviderType => IncidentManagementType.PagerDuty;

    // PagerDuty-specific fields (to be defined when implementing full support)
    public List<string> ServiceIds { get; set; } = new();
    public List<string> Urgencies { get; set; } = new() { "high", "low" };

    // Common fields from interface
    public int LookbackDays { get; set; } = 90;
    public int? MaxIncidents { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// PagerDuty-specific payload for configuration requests.
/// </summary>
public record PagerDutyIncidentIndexingConfigurationPayload : IncidentIndexingConfigurationPayload
{
    public List<string> ServiceIds { get; init; } = new();
    public List<string> Urgencies { get; init; } = new() { "high", "low" };
}
