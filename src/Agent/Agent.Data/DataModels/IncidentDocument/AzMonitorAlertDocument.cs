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

    public DateTime HandledAt { get; set; }

    public AlertProperties Properties { get; set; } = new AlertProperties();

    // Adding an optional alert rule resource ID for deduplication purposes.
    public string? AlertRuleResourceId { get; set; } = string.Empty;

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
            Status = essentials?.AlertState ?? string.Empty,
            IncidentType = alert.Properties?.Essentials?.SignalType ?? string.Empty,
            CreatedAt = DateTimeHelper.ParseDateTimeOffset(alert.Properties?.Essentials?.StartDateTime).UtcDateTime,
            TargetResourceId = essentials?.TargetResource ?? string.Empty,
            TargetResourceType = essentials?.TargetResourceType ?? string.Empty,
            SubscriptionId = alertIdentifier.SubscriptionId ?? string.Empty,
            Description = essentials?.Description ?? string.Empty,
            AlertRuleResourceId = essentials?.AlertRule ?? string.Empty,
            UpdatedAt = DateTime.UtcNow,
            HitCount = 1,
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
