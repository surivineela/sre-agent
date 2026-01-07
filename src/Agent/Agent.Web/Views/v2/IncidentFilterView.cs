// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Agent.Core.Configuration;
using Agent.Data.DataModels;
using Agent.Web.ApiResources;
using Agent.Web.Json;

namespace Agent.Web.Views.v2;

public record IcmFilterSettingsView
{
    public Settable<string> MonitorId { get; set; }
    public Settable<string> CreatedBy { get; set; } = string.Empty;
}

public record AzMonitorFilterSettingsView
{
    public Settable<string> TargetResourceType { get; set; } = string.Empty;
    public Settable<string> TargetResource { get; set; } = string.Empty;
}

public class IncidentFilterView
{
    // Shared fields by all incident filters
    // Icm, AzMonitor, PagerDuty, ServiceNow, etc.
    public Settable<string> IncidentPlatform { get; set; }
    public Settable<string> ImpactedService { get; set; }
    public Settable<string> Priority { get; set; }
    public Settable<string> IncidentType { get; set; }
    public Settable<string> AlertId { get; set; }
    public Settable<string> TitleContains { get; set; }
    public Settable<string> AgentMode { get; set; }
    public Settable<string> HandlingAgent { get; set; }
    public Settable<string> OwningTeamId { get; set; }

    /// <summary>
    /// Maximum number of automated investigation attempts for recurring alerts before requesting user input.
    /// When an alert fires repeatedly and automated RCA fails to find a definitive root cause,
    /// the agent will ask the user for additional context after this many attempts.
    /// </summary>
    public Settable<int?> MaxAutomatedInvestigationAttempts { get; set; }
    public Settable<bool?> DeepInvestigationEnabled { get; set; }

    public Settable<DateTime?> CreatedAt { get; set; }
    public Settable<DateTime?> UpdatedAt { get; set; }

    public Settable<bool?> IsEnabled { get; set; }
    // Custom settings for different incident management types
    // Only 1 of the following should be set
    public Settable<IcmFilterSettingsView> IcmFilterSettings { get; set; }
    public Settable<AzMonitorFilterSettingsView> AzMonitorFilterSettings { get; set; }

    public static ApiResponseEnvelope<IncidentFilterView> CreateApiResponseEnvelope(IIncidentFilterDocument document)
    {
        var view = new IncidentFilterView();


        // Map shared fields from the document payload
        if (document is IncidentFilterDocumentPayload payload)
        {
            view.ImpactedService = payload.ImpactedService;
            view.Priority = payload.Priority;
            view.IncidentType = payload.IncidentType;
            view.AlertId = payload.AlertId;
            view.TitleContains = payload.TitleContains;
            view.AgentMode = payload.AgentMode;
            view.HandlingAgent = payload.HandlingAgent;
            view.OwningTeamId = payload.OwningTeamId;
            view.MaxAutomatedInvestigationAttempts = payload.MaxAutomatedInvestigationAttempts;
            view.DeepInvestigationEnabled = payload.DeepInvestigationEnabled;
            view.CreatedAt = payload.CreatedAt;
            view.UpdatedAt = payload.UpdatedAt;
        }

        view.IsEnabled = document.IsEnabled;

        // Map type-specific settings
        switch (document)
        {
            case IcmIncidentFilterDocument icmDoc:
                view.IncidentPlatform = IncidentManagementType.Icm.ToString();
                view.IcmFilterSettings = new IcmFilterSettingsView
                {
                    MonitorId = icmDoc.MonitorId,
                    CreatedBy = icmDoc.CreatedBy
                };
                break;
            case AzMonitorIncidentFilterDocument azMonitorDoc:
                view.IncidentPlatform = IncidentManagementType.AzMonitor.ToString();
                view.AzMonitorFilterSettings = new AzMonitorFilterSettingsView
                {
                    TargetResourceType = azMonitorDoc.TargetResourceType,
                    TargetResource = azMonitorDoc.TargetResource
                };
                break;
            case PagerDutyIncidentFilterDocument:
                view.IncidentPlatform = IncidentManagementType.PagerDuty.ToString();
                break;
            case ServiceNowIncidentFilterDocument:
                view.IncidentPlatform = IncidentManagementType.ServiceNow.ToString();
                break;
        }

        return new ApiResponseEnvelope<IncidentFilterView>
        {
            Name = document.Id,
            Type = "IncidentFilter",
            Properties = view
        };
    }

    public static ApiResponseEnvelope<IncidentFilterView> CreateApiResponseEnvelope(string name, IncidentFilterView view)
    {
        return new ApiResponseEnvelope<IncidentFilterView>
        {
            Name = name,
            Type = "IncidentFilter",
            Properties = view
        };
    }

    public static IIncidentFilterDocument CreateModel(
        ApiRequestEnvelope<IncidentFilterView> envelope,
        ResourceMetadata? metadata = null,
        IIncidentFilterDocument? baseModel = null)
    {
        // Determine the incident management type from the envelope or base model
        IncidentManagementType? incidentType = null;

        envelope.Properties.ApplyTo(props =>
        {
            props?.IncidentPlatform.ApplyTo(typeStr =>
            {
                if (Enum.TryParse<IncidentManagementType>(typeStr, ignoreCase: true, out var parsed))
                {
                    incidentType = parsed;
                }
            });
        });

        // If type not in envelope, try to infer from baseModel
        if (incidentType == null && baseModel != null)
        {
            incidentType = baseModel switch
            {
                IcmIncidentFilterDocument => IncidentManagementType.Icm,
                AzMonitorIncidentFilterDocument => IncidentManagementType.AzMonitor,
                PagerDutyIncidentFilterDocument => IncidentManagementType.PagerDuty,
                ServiceNowIncidentFilterDocument => IncidentManagementType.ServiceNow,
                _ => null
            };
        }

        // Create or use the appropriate document type
        IIncidentFilterDocument result = incidentType switch
        {
            IncidentManagementType.Icm => baseModel as IcmIncidentFilterDocument ?? new IcmIncidentFilterDocument() { Id = envelope.Name.Value ?? metadata?.Name ?? "" },
            IncidentManagementType.AzMonitor => baseModel as AzMonitorIncidentFilterDocument ?? new AzMonitorIncidentFilterDocument() { Id = envelope.Name.Value ?? metadata?.Name ?? "" },
            IncidentManagementType.PagerDuty => baseModel as PagerDutyIncidentFilterDocument ?? new PagerDutyIncidentFilterDocument() { Id = envelope.Name.Value ?? metadata?.Name ?? "" },
            IncidentManagementType.ServiceNow => baseModel as ServiceNowIncidentFilterDocument ?? new ServiceNowIncidentFilterDocument() { Id = envelope.Name.Value ?? metadata?.Name ?? "" },
            _ => baseModel ?? throw new ArgumentException("Unable to determine incident filter type")
        };

        // Apply metadata if provided
        if (metadata != null)
        {
            result.CreatedAt = metadata.CreatedAt ?? result.CreatedAt;
            result.UpdatedAt = DateTime.UtcNow;
        }
        else
        {
            result.UpdatedAt = DateTime.UtcNow;
        }

        // Apply properties from envelope
        envelope.Properties.ApplyTo(properties =>
        {
            if (properties == null) return;

            // Apply shared fields
            if (result is IncidentFilterDocumentPayload payload)
            {
                properties.ImpactedService.ApplyTo(value => payload.ImpactedService = value ?? string.Empty);
                properties.Priority.ApplyTo(value => payload.Priority = value ?? string.Empty);
                properties.IncidentType.ApplyTo(value => payload.IncidentType = value ?? string.Empty);
                properties.AlertId.ApplyTo(value => payload.AlertId = value ?? string.Empty);
                properties.TitleContains.ApplyTo(value => payload.TitleContains = value ?? string.Empty);
                properties.AgentMode.ApplyTo(value => payload.AgentMode = value ?? string.Empty);
                properties.HandlingAgent.ApplyTo(value => payload.HandlingAgent = value ?? string.Empty);
                properties.OwningTeamId.ApplyTo(value => payload.OwningTeamId = value ?? string.Empty);
                properties.MaxAutomatedInvestigationAttempts.ApplyTo(value => payload.MaxAutomatedInvestigationAttempts = value ?? 3);
                properties.DeepInvestigationEnabled.ApplyTo(value => payload.DeepInvestigationEnabled = value ?? false);
                properties.CreatedAt.ApplyTo(value => payload.CreatedAt = value ?? DateTime.UtcNow);
                properties.UpdatedAt.ApplyTo(value => payload.UpdatedAt = value ?? DateTime.UtcNow);
            }

            properties.IsEnabled.ApplyTo(value => result.IsEnabled = value ?? true);

            // Apply type-specific settings
            switch (result)
            {
                case IcmIncidentFilterDocument icmDoc:
                    properties.IcmFilterSettings.ApplyTo(settings =>
                    {
                        settings?.MonitorId.ApplyTo(value => icmDoc.MonitorId = value ?? string.Empty);
                        settings?.CreatedBy.ApplyTo(value => icmDoc.CreatedBy = value ?? string.Empty);
                    });
                    break;
                case AzMonitorIncidentFilterDocument azMonitorDoc:
                    properties.AzMonitorFilterSettings.ApplyTo(settings =>
                    {
                        settings?.TargetResourceType.ApplyTo(value => azMonitorDoc.TargetResourceType = value ?? string.Empty);
                        settings?.TargetResource.ApplyTo(value => azMonitorDoc.TargetResource = value ?? string.Empty);
                    });
                    break;
            }
        });

        return result;
    }
}

