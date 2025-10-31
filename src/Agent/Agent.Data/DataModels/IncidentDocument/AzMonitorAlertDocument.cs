// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data.DataModels.IncidentModel;
using Agent.Data.Helpers;
using Azure.Core;

namespace Agent.Data.DataModels;

public record AzMonitorAlertDocument : IIncidentDocument
{
    public static string ContainerName => AgentDataConfiguration.ThreadContainerName;

    public string DocumentType => $"{nameof(IncidentManagementType.AzMonitor)}Incident";

    public string Id { get; init; } = string.Empty;

    public string AlertId { get; set; } = string.Empty;

    public string PartitionKey => Id;

    public string Title { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime LastModifiedTime { get; set; } = DateTime.UtcNow;

    public string TargetResourceType { get; set; } = string.Empty;

    public string TargetResourceId { get; set; } = string.Empty;

    public string SubscriptionId { get; set; } = string.Empty;

    public int HitCount { get; set; } = 1;

    public bool UserInputRequested { get; set; } = false;

    public bool TargetResourceInputRequested { get; set; } = false;

    public string ImpactedServiceId { get; set; } = string.Empty;

    public string ImpactedServiceName { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string IncidentType { get; set; } = string.Empty;

    public string Priority { get; set; } = string.Empty;

    public string Severity { get; set; } = string.Empty;

    public string ExtractedKnowledge { get; set; } = string.Empty;

    public string AIRootCause { get; set; } = string.Empty;

    public string RootCauseDescription { get; set; } = string.Empty;

    public string GeneralSummary { get; set; } = string.Empty;

    public bool IsAssistedByAgent { get; set; } = false;

    public DateTime? ResolvedAt { get; set; } = null;

    public AlertProperties Properties { get; set; } = new AlertProperties();

    // Adding an optional alert rule resource ID for deduplication purposes.
    public string? AlertRuleResourceId { get; set; } = string.Empty;

    public List<string> Tags { get; set; } = new List<string>();

    public static AzMonitorAlertDocument FromIncident(AlertItem alert)
    {
        var essentials = alert.Properties.Essentials;
        var alertIdentifier = new ResourceIdentifier(alert.Id);
        var alertRuleResourceId = new ResourceIdentifier(essentials.AlertRule);
        return new AzMonitorAlertDocument()
        {
            Id = alertIdentifier.Name ?? string.Empty, // ['/', '\', '#'] cannot be used in cosmos db document ID, so only using the alert guid
            AlertId = alert.Id, // resource Id for the fired alert
            Title = alertRuleResourceId.Name ?? string.Empty, // only get the Alert Rule Name
            Properties = alert.Properties,
            Priority = essentials?.Severity ?? string.Empty,
            Status = MapToIncidentStatus(essentials),
            IncidentType = alert.Properties?.Essentials?.SignalType ?? string.Empty,
            CreatedAt = DateTimeHelper.ParseDateTimeOffset(alert.Properties?.Essentials?.StartDateTime).UtcDateTime,
            TargetResourceId = essentials?.TargetResource ?? string.Empty,
            TargetResourceType = essentials?.TargetResourceType ?? string.Empty,
            SubscriptionId = alertIdentifier.SubscriptionId ?? string.Empty,
            Description = essentials?.Description ?? string.Empty,
            AlertRuleResourceId = essentials?.AlertRule ?? string.Empty,
            UpdatedAt = DateTime.UtcNow,
            LastModifiedTime = DateTimeHelper.ParseDateTimeOffset(alert.Properties?.Essentials?.LastModifiedDateTime).UtcDateTime,
            HitCount = 1,
        };
    }

    /// <summary>
    /// Maps Azure Monitor alert essentials to incident status following the escalation flow:
    /// "new" → "acknowledged" → "resolved" → "closed"
    /// 
    /// MonitorCondition: "Fired" or "Resolved" (indicates if alert condition is active)
    /// AlertState: "New", "Acknowledged", or "Closed" (manual state management by operators)
    /// </summary>
    private static string MapToIncidentStatus(AlertEssentials? essentials)
    {
        if (essentials == null)
        {
            return string.Empty;
        }

        var monitorCondition = essentials.MonitorCondition?.ToLowerInvariant();
        var alertState = essentials.AlertState?.ToLowerInvariant();

        // Use tuple pattern matching for cleaner logic
        return (alertState, monitorCondition) switch
        {
            // Highest priority: AlertState = "closed" is the final terminal state
            ("closed", _) => "closed",
            
            // Second priority: MonitorCondition = "resolved" means the condition has cleared
            (_, "resolved") => "resolved",
            
            // Third priority: AlertState = "acknowledged" means operator has acknowledged
            ("acknowledged", _) => "acknowledged",
            
            // Fourth priority: Either "new" AlertState or "fired" MonitorCondition means active/new
            ("new", _) => "new",
            (_, "fired") => "new",
            
            // Fallback: prefer AlertState over MonitorCondition, then empty string
            _ => alertState ?? monitorCondition ?? string.Empty
        };
    }

    public static AlertItem ToIncidentItem(AzMonitorAlertDocument incident)
    {
        return new AlertItem()
        {
            Id = incident.AlertId,
            Name = incident.Title,
            Properties = incident.Properties,
            Type = "Microsoft.AlertsManagement/alerts"
        };
    }
}

public enum AzMonitorIncidentStatus
{
    New,
    Acknowledged,
    Resolved,
    Closed
}
