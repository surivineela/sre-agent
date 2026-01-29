// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Data.DataModels;

/// <summary>
/// Base payload class with common fields for incident indexing configuration requests.
/// Provider-specific payloads inherit from this class.
/// Uses polymorphic JSON deserialization to route to correct derived type.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "providerType")]
[JsonDerivedType(typeof(IcmIncidentIndexingConfigurationPayload), "icm")]
[JsonDerivedType(typeof(PagerDutyIncidentIndexingConfigurationPayload), "pagerduty")]
[JsonDerivedType(typeof(ServiceNowIncidentIndexingConfigurationPayload), "servicenow")]
[JsonDerivedType(typeof(AzMonitorIncidentIndexingConfigurationPayload), "azmonitor")]
public record IncidentIndexingConfigurationPayload
{
    /// <summary>
    /// Number of days to look back when indexing incidents.
    /// </summary>
    public int LookbackDays { get; init; } = 90;

    /// <summary>
    /// Maximum number of incidents to index (null = no limit).
    /// </summary>
    public int? MaxIncidents { get; init; }
}
