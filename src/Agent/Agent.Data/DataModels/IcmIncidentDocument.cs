using System.Diagnostics.CodeAnalysis;
using Agent.Core.Models.ICM;
using Azure.ResourceManager.AppService.Models;

namespace Agent.Data.DataModels;
public class IcmIncidentDocument : Incident, IIncidentDocument
{

    public IcmIncidentDocument()
    {
        // Default constructor for serialization
    }

    [SetsRequiredMembers]
    public IcmIncidentDocument(Incident incident)
    {
        //coping all properties from Incident to base class
        IncidentId = incident.IncidentId;
        CloudInstance = incident.CloudInstance;
        Slice = incident.Slice;
        HitCount = incident.HitCount;
        ParentIncidentId = incident.ParentIncidentId;
        Environment = incident.Environment;
        CreatedBy = incident.CreatedBy;
        ImpactStartDate = incident.ImpactStartDate;
        CreatedDate = incident.CreatedDate;
        LastModifiedDate = incident.LastModifiedDate;
        OwningService = incident.OwningService;
        OwningServiceId = incident.OwningServiceId;
        OwningTeam = incident.OwningTeam;
        OwningTeamName = incident.OwningTeamName;
        Owner = incident.Owner;
        Severity = incident.Severity;
        Title = incident.Title;
        Keywords = incident.Keywords;
        Summary = incident.Summary;
        DiscussionEntry = incident.DiscussionEntry;
        MonitoringRole = incident.MonitoringRole;
        MonitoringSlice = incident.MonitoringSlice;
        SubscriptionId = incident.SubscriptionId;
        Tags = incident.Tags;
        Stamp = incident.Stamp;
        Datacenter = incident.Datacenter;
        MitigateData = incident.MitigateData;
        Status = incident.Status;
        IncidentType = incident.IncidentType;

        //Overwrite with few properties that added for IIncidentDocument
        Id = incident.IncidentId;
        CreatedAt = incident.CreatedDate;
        UpdatedAt = incident.LastModifiedDate;
        Description = incident.Summary;
        Priority = incident.Severity;
    }

    public static string ContainerName => AgentDataConfiguration.ThreadContainerName; // Cosmos DB container name
    public string DocumentType => "IcmIncident";

    public string Id { get; init; } = string.Empty;

    public string PartitionKey => Id; // Use incident id as partition key

    public string Description { get; set; } = string.Empty;

    string IIncidentDocument.Status
    {
        get => Status.ToString();
        set => Status = Enum.TryParse(value, true, out IncidentStatus status) ? status : IncidentStatus.Active;
    }

    string IIncidentDocument.IncidentType
    {
        get => IncidentType.ToString();
        set => IncidentType = Enum.TryParse(value, true, out IncidentType incidentType) ? incidentType : IncidentType.LiveSite;
    }

    public string Priority { get; set; } = string.Empty;
    public string ImpactedServiceId { get; set; } = string.Empty;

    public string ImpactedServiceName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; set; }
    public List<DiscussionEntry> DiscussionEntries { get; set; } = new List<DiscussionEntry>();

    public string ExtractedKnowledge { get; set; } = string.Empty;

    public DateTime? MitigatedAt { get; set; } = null;

    public DateTime? ResolvedAt { get; set; } = null;

    public string RootCause { get; set; } = string.Empty;

    public string GeneralSummary { get; set; } = string.Empty;

    public DateTime HandledAt { get; set; }

    public static IcmIncidentDocument TruncateIcmIncidentDocument(Incident incident)
    {
        int maxSummaryLength = 32 * 1024; // Define a maximum length for the summary
        string summary = incident.Summary ?? string.Empty;
        if (summary.Length > maxSummaryLength)
        {
            summary = summary.Substring(0, maxSummaryLength) + "... [TRUNCATED]";
        }

        return new IcmIncidentDocument(incident)
        {
            Summary = summary
        };
    }
}
