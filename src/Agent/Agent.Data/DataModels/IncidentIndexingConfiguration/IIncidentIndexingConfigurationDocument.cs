// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;
using Agent.Core.Configuration;

namespace Agent.Data.DataModels;

/// <summary>
/// Base interface for all incident indexing configuration documents.
/// Each provider (ICM, PagerDuty, ServiceNow, AzMonitor) implements this interface.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "providerType")]
[JsonDerivedType(typeof(IcmIncidentIndexingConfigurationDocument), "icm")]
[JsonDerivedType(typeof(PagerDutyIncidentIndexingConfigurationDocument), "pagerduty")]
[JsonDerivedType(typeof(ServiceNowIncidentIndexingConfigurationDocument), "servicenow")]
[JsonDerivedType(typeof(AzMonitorIncidentIndexingConfigurationDocument), "azmonitor")]
public interface IIncidentIndexingConfigurationDocument : ICosmosDocument
{
    static string ICosmosDocument.ContainerName => AgentDataConfiguration.ThreadContainerName;

    /// <summary>
    /// The incident management provider type.
    /// </summary>
    IncidentManagementType ProviderType { get; }

    /// <summary>
    /// Number of days to look back when indexing incidents.
    /// </summary>
    int LookbackDays { get; set; }

    /// <summary>
    /// Maximum number of incidents to index (null = no limit).
    /// </summary>
    int? MaxIncidents { get; set; }

    /// <summary>
    /// Timestamp when the configuration was created.
    /// </summary>
    DateTime CreatedAt { get; set; }

    /// <summary>
    /// Timestamp when the configuration was last updated.
    /// </summary>
    DateTime UpdatedAt { get; set; }
}
