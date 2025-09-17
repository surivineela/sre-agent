// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.Text.Json.Serialization;

namespace Agent.Data.DataModels.IncidentModel;

// Classes to deserialize the REST API response
public class AlertsResponse
{
    [JsonPropertyName("value")]
    public required List<AlertItem> Value { get; set; }
}

public class AlertItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("properties")]
    public AlertProperties Properties { get; set; } = new AlertProperties();
}

public class AlertProperties
{
    [JsonPropertyName("essentials")]
    public AlertEssentials Essentials { get; set; } = new AlertEssentials();
}

public class AlertEssentials
{
    [JsonPropertyName("actionStatus")]
    public ActionStatus ActionStatus { get; set; } = new ActionStatus();

    [JsonPropertyName("alertRule")]
    public string AlertRule { get; set; } = string.Empty;

    [JsonPropertyName("alertState")]
    public string AlertState { get; set; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    [JsonPropertyName("lastModifiedDateTime")]
    public string LastModifiedDateTime { get; set; } = string.Empty;

    [JsonPropertyName("lastModifiedUserName")]
    public string LastModifiedUserName { get; set; } = string.Empty;

    [JsonPropertyName("monitorCondition")]
    public string MonitorCondition { get; set; } = string.Empty;

    [JsonPropertyName("monitorService")]
    public string MonitorService { get; set; } = string.Empty;

    [JsonPropertyName("severity")]
    public string Severity { get; set; } = string.Empty;

    [JsonPropertyName("signalType")]
    public string SignalType { get; set; } = string.Empty;

    [JsonPropertyName("sourceCreatedId")]
    public string SourceCreatedId { get; set; } = string.Empty;

    [JsonPropertyName("startDateTime")]
    public string StartDateTime { get; set; } = string.Empty;

    [JsonPropertyName("targetResource")]
    public string TargetResource { get; set; } = string.Empty;

    [JsonPropertyName("targetResourceGroup")]
    public string TargetResourceGroup { get; set; } = string.Empty;

    [JsonPropertyName("targetResourceName")]
    public string TargetResourceName { get; set; } = string.Empty;

    [JsonPropertyName("targetResourceType")]
    public string TargetResourceType { get; set; } = string.Empty;
}

public class ActionStatus
{
    [JsonPropertyName("isSuppressed")]
    public bool IsSuppressed { get; set; }
}
