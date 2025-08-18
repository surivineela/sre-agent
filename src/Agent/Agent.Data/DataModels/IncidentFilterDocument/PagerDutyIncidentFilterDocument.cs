namespace Agent.Data.DataModels;
public record PagerDutyIncidentFilterDocument: IncidentFilterDocument
{
    public PagerDutyIncidentFilterDocument(
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
        string OwningTeamId = ""
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
    ){ }
}
public class PagerDutyIncidentFilterDocumentPayload : IncidentFilterDocumentPayload { }
