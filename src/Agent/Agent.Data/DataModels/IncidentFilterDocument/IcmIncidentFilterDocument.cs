namespace Agent.Data.DataModels;
public record IcmIncidentFilterDocument : IncidentFilterDocument
{

    public IcmIncidentFilterDocument(
        string Id, // Filter Id
        string DocumentType,
        DateTime CreatedAt,
        string Name,
        string ImpactedService,
        string Priority,
        string IncidentType,
        string AlertId,
        string TitleContains,
        bool IsEnabled = true,
        string AgentMode = "",
        string OwningTeamId = "",
        string CreatedBy = "",
        string MonitorId = ""
    ) : base(
        Id,
        DocumentType,
        CreatedAt,
        Name,
        ImpactedService,
        Priority,
        IncidentType,
        AlertId,
        TitleContains,
        IsEnabled,
        AgentMode,
        OwningTeamId
    )
    {
        this.MonitorId = MonitorId;
        this.CreatedBy = CreatedBy;
    }
    public string MonitorId { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}

public class IcmIncidentFilterDocumentPayload : IncidentFilterDocumentPayload
{
    public string MonitorId { get; set; } = string.Empty;
    public string CreatedBy { get; set; } = string.Empty;
}
