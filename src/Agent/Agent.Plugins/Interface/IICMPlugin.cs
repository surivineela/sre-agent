using Agent.Core.Models.ICM;

namespace Agent.Plugins.Interface;
public interface IICMPlugin
{
    Task<Incident> GetIncidentInfo(string incidentId);
    Task<List<CustomField>> GetCustomFields(string incidentId);
    Task<string> SearchIncidents(string searchString, int lookbackPeriodInDays, int resultCountLimit);
    string GetCurrentUtcDateTime();
    string GetIcmCorrelationAndLinkingRules();
    Task<DiscussionEntry?> GetAlertingDiscussionEntry(string incidentId);
    Task<List<DiscussionEntry>> GetDiscussionEntries(string incidentId);
    Task<string> TransferIncident(string incidentId, string discussionEntry, string tenantName, string owningTeam);
    Task<string> MitigateIncident(string incidentId, string discussionEntry);
    Task<string> DowngradeSeverity(string incidentId, string discussionEntry);
    Task<string> ResolveIncident(string incidentId, string discussionEntry);
    Task<string> PostDiscussionEntry(string incidentId, string discussionEntry);
    Task<string> AddTagToIncident(string incidentId, string tag);
    Task<string> AddKeywordToIncident(string incidentId, string keyword);
    Task<string> AcknowledgeIncident(string incidentId);
    Task<List<IncidentRepairItem>> GetIncidentRepairItems(long incidentId);
    Task<List<string>> GetLinkedRelatedIncidentInfo(long incidentId);
    Task<string> AddRelatedIncidentLink(long incidentId, long relatedIncidentId);
    Task<string> RemoveRelatedIncidentLink(long incidentId, long relatedIncidentId);
    Task<string> GetParentIncidentInfo(long incidentId);
    Task<string> AddParentIncidentLink(long incidentId, long parentIncidentId);
    Task<string> RemoveParentIncidentLink(long incidentId);
    Task<List<string>> GetChildIncidentsInfo(long incidentId);
    Task<string> GetParametersFromIncident(string incidentId, string instruction);
    Task<string> AddIncidentAttachment(string incidentId, string filePath);
}
