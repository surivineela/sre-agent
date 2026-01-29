// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

/// <summary>
/// Azure Monitor-specific incident indexing configuration document.
/// Stored in CosmosDB with provider-discriminated ID.
/// </summary>
public class AzMonitorIncidentIndexingConfigurationDocument : IIncidentIndexingConfigurationDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public string Id { get; set; } = IncidentIndexingConfigurationUtilities.GetDocumentId(IncidentManagementType.AzMonitor);

    public string DocumentType => IncidentIndexingConfigurationUtilities.GetDocumentTypeName(IncidentManagementType.AzMonitor);

    public string PartitionKey => DocumentType;

    public IncidentManagementType ProviderType => IncidentManagementType.AzMonitor;

    // Azure Monitor-specific fields (to be defined when implementing full support)
    public List<string> ResourceGroups { get; set; } = new();
    public List<string> Severities { get; set; } = new() { "Sev0", "Sev1", "Sev2", "Sev3" };
    public List<string> AlertTypes { get; set; } = new();

    // Common fields from interface
    public int LookbackDays { get; set; } = 90;
    public int? MaxIncidents { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Azure Monitor-specific payload for configuration requests.
/// </summary>
public record AzMonitorIncidentIndexingConfigurationPayload : IncidentIndexingConfigurationPayload
{
    public List<string> ResourceGroups { get; init; } = new();
    public List<string> Severities { get; init; } = new() { "Sev0", "Sev1", "Sev2", "Sev3" };
    public List<string> AlertTypes { get; init; } = new();
}
