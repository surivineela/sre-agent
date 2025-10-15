// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using Microsoft.AzureAd.Icm.Types;
using Incident = Microsoft.SREAgent.Incidents.IcM.Model.ICMIncident;

namespace Agent.Data.DataModels;

public class IcmIncidentDocument : Incident, IIncidentDocument
{

    public IcmIncidentDocument()
    {
        // Default constructor for serialization
    }

    public IcmIncidentDocument(Incident incident)
    {
        Id = incident.Id.ToString();
        Title = incident.Title;
        Description = incident.Description;
        CreatedDate = incident.CreatedDate;
        LastModifiedDate = incident.LastModifiedDate;
        OccuringLocation = incident.OccuringLocation;
        SuppressionRuleApplied = incident.SuppressionRuleApplied;
        TsgInfo = incident.TsgInfo;
        TsgLink = incident.TsgLink;
        IsSecurityRisk = incident.IsSecurityRisk;
        IsCustomerImpacting = incident.IsCustomerImpacting;
        IsNoise = incident.IsNoise;
        State = incident.State;
        Severity = incident.Severity;
        Type = incident.Type;
        SubType = incident.SubType;
        ResponsibleServiceId = incident.ResponsibleServiceId;
        ResponsibleTeamId = incident.ResponsibleTeamId;
        OwningTeamId = incident.OwningTeamId;
        OwningServiceId = incident.OwningServiceId;
        OriginatingServiceId = incident.OriginatingServiceId;
        AssignedTo = incident.AssignedTo;
        CreatedBy = incident.CreatedBy;
        ModifiedTime = incident.ModifiedTime;
        ModifiedBy = incident.ModifiedBy;
        AcknowledgeTime = incident.AcknowledgeTime;
        AcknowledgeBy = incident.AcknowledgeBy;
        IsAcknowledged = incident.IsAcknowledged;
        IsAcknowledgeable = incident.IsAcknowledgeable;
        LastTransferTime = incident.LastTransferTime;
        LastActivateTime = incident.LastActivateTime;
        Keywords = incident.Keywords;
        RoutingId = incident.RoutingId;
        CorrelationId = incident.CorrelationId;
        MonitorName = incident.MonitorName;
        LinkedIncidentCount = incident.LinkedIncidentCount;
        ExternalLinksCount = incident.ExternalLinksCount;
        SourceCreateTime = incident.SourceCreateTime;
        HitCount = incident.HitCount;
        ImpactStartTime = incident.ImpactStartTime;
        CommitTime = incident.CommitTime;
        ChildCount = incident.ChildCount;
        MitigateTime = incident.MitigateTime;
        MitigateData = incident.MitigateData;
        ParentId = incident.ParentId;
        CustomerName = incident.CustomerName;
        LastCorrelationTime = incident.LastCorrelationTime;
        SourceOrigin = incident.SourceOrigin;
        SourceIncidentId = incident.SourceIncidentId;
        HowFixed = incident.HowFixed;
        CloudInstanceId = incident.CloudInstanceId;
        ServiceCategoryId = incident.ServiceCategoryId;
        SubscriptionId = incident.SubscriptionId;
        MonitorId = incident.MonitorId;
        Postmortem = incident.Postmortem;
        PublicPirId = incident.PublicPirId;
        MonitorLocation = incident.MonitorLocation;
        ResolveData = incident.ResolveData;
        ImpactedServices = incident.ImpactedServices;
        ImpactedTeams = incident.ImpactedTeams;
        TrackingTeams = incident.TrackingTeams;
        ImpactedComponents = incident.ImpactedComponents;
        Attachments = incident.Attachments;
        RootCause = incident.RootCause;
        AlertSource = incident.AlertSource;
        CustomFields = incident.CustomFields;
        IncidentOutage = incident.IncidentOutage;
        IncidentOutageNote = incident.IncidentOutageNote;
        Bridges = incident.Bridges;
        OutageServiceImpactTrackers = incident.OutageServiceImpactTrackers;
        OutageInvestigationWorkstreamItems = incident.OutageInvestigationWorkstreamItems;
        RootCauseOption = incident.RootCauseOption;
        SupportTicketId = incident.SupportTicketId;
        NotificationStatus = incident.NotificationStatus;
        PastOwningServices = incident.PastOwningServices;
        IncidentManagerContactId = incident.IncidentManagerContactId;
        SiteReliabilityContactId = incident.SiteReliabilityContactId;
        ChildIncidents = incident.ChildIncidents;
        ParentIncident = incident.ParentIncident;
        CausedIncidents = incident.CausedIncidents;
        ResponsibleIncidents = incident.ResponsibleIncidents;
        RelatedIncidents = incident.RelatedIncidents;
        ExternalLinks = incident.ExternalLinks;
        RepairItems = incident.RepairItems;
        HealthResourceId = incident.HealthResourceId;
        DiagnosticsLink = incident.DiagnosticsLink;
        IsOutage = incident.IsOutage;
        OutageDeclaredDate = incident.OutageDeclaredDate;
        ChangeList = incident.ChangeList;
        HistoryEntries = incident.HistoryEntries;
        Notifications = incident.Notifications;
        IsReadonly = incident.IsReadonly;
        OwningTeamName = incident.OwningTeamName;
        OwningTenantName = incident.OwningTenantName;
        ContactAlias = incident.ContactAlias;
        Summary = incident.Summary;
        IncidentManagerAlias = incident.IncidentManagerAlias;
        Tags = incident.Tags;


        //ICM Incident Document specific info
        CreatedAt = DateTime.UtcNow;
    }

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName; // Cosmos DB container name
    public string DocumentType => "IcmIncident";

    public string PartitionKey => Id; // Use incident id as partition key

    public string Priority => Severity.ToString();
    public string ImpactedServiceId { get; set; } = string.Empty;

    public string ImpactedServiceName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; set; }

    public List<DescriptionEntry> DiscussionEntries { get; set; } = new List<DescriptionEntry>();

    public string ExtractedKnowledge { get; set; } = string.Empty;

    public DateTime? MitigatedAt => MitigateData?.MitigateTime?.UtcDateTime;

    public DateTime? ResolvedAt => ResolveData?.ResolveTime?.UtcDateTime;

    public string GeneralSummary { get; set; } = string.Empty;

    public DateTime HandledAt { get; set; }

    public string Status => State;

    public string IncidentType => Type;

    public new string Id
    {
        get => base.Id.ToString();
        set
        {
            if (long.TryParse(value, out var longId))
            {
                base.Id = longId;
            }
            else
            {
                throw new ArgumentException("Invalid long format for Id");
            }
        }
    }

    public string AIRootCause { get; set; } = string.Empty;

    public static IcmIncidentDocument TruncateIcmIncidentDocument(Incident incident)
    {
        int maxSummaryLength = 32 * 1024; // Define a maximum length for the summary
        string summary = incident.Summary ?? string.Empty;
        if (summary.Length > maxSummaryLength)
        {
            summary = summary.Substring(0, maxSummaryLength) + "... [TRUNCATED]";
        }

        var incidentDoc = new IcmIncidentDocument(incident);
        incidentDoc.Summary = summary;
        return incidentDoc;
    }
}
