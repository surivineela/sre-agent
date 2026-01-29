// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

/// <summary>
/// Base response class for incident indexing configuration API responses.
/// Uses polymorphic JSON serialization to return provider-specific responses.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "providerType")]
[JsonDerivedType(typeof(IcmIncidentIndexingConfigurationResponse), "icm")]
[JsonDerivedType(typeof(PagerDutyIncidentIndexingConfigurationResponse), "pagerduty")]
[JsonDerivedType(typeof(ServiceNowIncidentIndexingConfigurationResponse), "servicenow")]
[JsonDerivedType(typeof(AzMonitorIncidentIndexingConfigurationResponse), "azmonitor")]
public abstract record IncidentIndexingConfigurationResponse
{
    /// <summary>
    /// Number of days to look back when indexing incidents.
    /// </summary>
    public int LookbackDays { get; init; }

    /// <summary>
    /// Maximum number of incidents to index (null = no limit).
    /// </summary>
    public int? MaxIncidents { get; init; }

    /// <summary>
    /// Timestamp when the configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Timestamp when the configuration was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; init; }
}

/// <summary>
/// ICM-specific configuration response.
/// </summary>
public record IcmIncidentIndexingConfigurationResponse : IncidentIndexingConfigurationResponse
{
    public List<string> TeamIds { get; init; } = new();
    public int MinSeverity { get; init; }
    public int MaxSeverity { get; init; }
    public List<string> IncidentTypes { get; init; } = new();
    public string? TeamName { get; init; }
    public string? TenantName { get; init; }

    /// <summary>
    /// Creates a response from a document.
    /// </summary>
    public static IcmIncidentIndexingConfigurationResponse FromDocument(IcmIncidentIndexingConfigurationDocument doc)
    {
        return new IcmIncidentIndexingConfigurationResponse
        {
            TeamIds = doc.TeamIds,
            MinSeverity = doc.MinSeverity,
            MaxSeverity = doc.MaxSeverity,
            IncidentTypes = doc.IncidentTypes,
            TeamName = doc.TeamName,
            TenantName = doc.TenantName,
            LookbackDays = doc.LookbackDays,
            MaxIncidents = doc.MaxIncidents,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt
        };
    }
}

/// <summary>
/// PagerDuty-specific configuration response.
/// </summary>
public record PagerDutyIncidentIndexingConfigurationResponse : IncidentIndexingConfigurationResponse
{
    public List<string> ServiceIds { get; init; } = new();
    public List<string> Urgencies { get; init; } = new();

    /// <summary>
    /// Creates a response from a document.
    /// </summary>
    public static PagerDutyIncidentIndexingConfigurationResponse FromDocument(PagerDutyIncidentIndexingConfigurationDocument doc)
    {
        return new PagerDutyIncidentIndexingConfigurationResponse
        {
            ServiceIds = doc.ServiceIds,
            Urgencies = doc.Urgencies,
            LookbackDays = doc.LookbackDays,
            MaxIncidents = doc.MaxIncidents,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt
        };
    }
}

/// <summary>
/// ServiceNow-specific configuration response.
/// </summary>
public record ServiceNowIncidentIndexingConfigurationResponse : IncidentIndexingConfigurationResponse
{
    public string? AssignmentGroup { get; init; }
    public List<string> Categories { get; init; } = new();
    public List<int> Priorities { get; init; } = new();

    /// <summary>
    /// Creates a response from a document.
    /// </summary>
    public static ServiceNowIncidentIndexingConfigurationResponse FromDocument(ServiceNowIncidentIndexingConfigurationDocument doc)
    {
        return new ServiceNowIncidentIndexingConfigurationResponse
        {
            AssignmentGroup = doc.AssignmentGroup,
            Categories = doc.Categories,
            Priorities = doc.Priorities,
            LookbackDays = doc.LookbackDays,
            MaxIncidents = doc.MaxIncidents,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt
        };
    }
}

/// <summary>
/// Azure Monitor-specific configuration response.
/// </summary>
public record AzMonitorIncidentIndexingConfigurationResponse : IncidentIndexingConfigurationResponse
{
    public List<string> ResourceGroups { get; init; } = new();
    public List<string> Severities { get; init; } = new();
    public List<string> AlertTypes { get; init; } = new();

    /// <summary>
    /// Creates a response from a document.
    /// </summary>
    public static AzMonitorIncidentIndexingConfigurationResponse FromDocument(AzMonitorIncidentIndexingConfigurationDocument doc)
    {
        return new AzMonitorIncidentIndexingConfigurationResponse
        {
            ResourceGroups = doc.ResourceGroups,
            Severities = doc.Severities,
            AlertTypes = doc.AlertTypes,
            LookbackDays = doc.LookbackDays,
            MaxIncidents = doc.MaxIncidents,
            CreatedAt = doc.CreatedAt,
            UpdatedAt = doc.UpdatedAt
        };
    }
}
