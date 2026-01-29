// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

/// <summary>
/// ICM-specific incident indexing configuration document.
/// Stored in CosmosDB with provider-discriminated ID.
/// </summary>
public class IcmIncidentIndexingConfigurationDocument : IIncidentIndexingConfigurationDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    /// <summary>
    /// Provider-discriminated ID for ICM configuration.
    /// </summary>
    public string Id { get; set; } = IncidentIndexingConfigurationUtilities.GetDocumentId(IncidentManagementType.Icm);

    public string DocumentType => IncidentIndexingConfigurationUtilities.GetDocumentTypeName(IncidentManagementType.Icm);

    public string PartitionKey => DocumentType;

    public IncidentManagementType ProviderType => IncidentManagementType.Icm;

    // ICM-specific fields
    public List<string> TeamIds { get; set; } = new();
    public int MinSeverity { get; set; } = 0;
    public int MaxSeverity { get; set; } = 3;
    public List<string> IncidentTypes { get; set; } = new() { "LiveSite", "CustomerReported" };

    /// <summary>
    /// Validated team name from ICM (populated after validation).
    /// </summary>
    public string? TeamName { get; set; }

    /// <summary>
    /// Validated tenant name from ICM (populated after validation).
    /// </summary>
    public string? TenantName { get; set; }

    // Common fields from interface
    public int LookbackDays { get; set; } = 90;
    public int? MaxIncidents { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// ICM-specific payload for configuration requests.
/// </summary>
public record IcmIncidentIndexingConfigurationPayload : IncidentIndexingConfigurationPayload
{
    public List<string> TeamIds { get; init; } = new();
    public int MinSeverity { get; init; } = 0;
    public int MaxSeverity { get; init; } = 3;
    public List<string> IncidentTypes { get; init; } = new() { "LiveSite", "CustomerReported" };
}
