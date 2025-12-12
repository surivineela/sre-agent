using Agent.Plugins.Implementation;
using Microsoft.AzureAd.Icm.IcmV3OData.Models;
using Microsoft.AzureAd.Icm.Types;
using Microsoft.SREAgent.Incidents.IcM.Model;
using Attachment = Microsoft.AzureAd.Icm.IcmV3OData.Models.Attachment;

namespace Agent.Plugins.Interface;

public interface IICMPlugin
{
    InvestigationTimeRangeResult GetIssueInvestigationTimeRange(DateTime? issueFirstOccurrence, DateTime? issueLastOccurrence, DateTime? reportedIssueObservedOnTime);

    Task<ICMIncident> GetIncidentInfo(string incidentId);

    Task<List<CustomField>> GetCustomFields(string incidentId);

    Task<string> SearchIncidents(string searchString, int lookbackPeriodInDays, int resultCountLimit);

    string GetCurrentUtcDateTime();

    string GetIcmCorrelationAndLinkingRules();

    Task<DescriptionEntry?> GetAlertingDiscussionEntry(string incidentId);

    Task<List<DescriptionEntry>> GetDiscussionEntries(string incidentId);

    Task<List<DescriptionEntry>> GetTopDiscussionEntries(string incidentId, uint? limit, bool IsAscending = true);

    Task<string> TransferIncident(string incidentId, string discussionEntry, string tenantName, string owningTeam);

    Task<string> MitigateIncident(string incidentId, string discussionEntry);

    Task<string> DowngradeSeverity(string incidentId, string discussionEntry);

    Task<string> UpdateIncidentSeverity(string incidentId, int severity, string discussionEntry);

    Task<string> ResolveIncident(string incidentId, string discussionEntry);

    Task<string> PostDiscussionEntry(string incidentId, string discussionEntry);

    Task<string> AddTagToIncident(string incidentId, string tag);

    Task<string> AddKeywordToIncident(string incidentId, string keyword);

    Task<string> AcknowledgeIncident(string incidentId);

    Task<List<ExternalLink>> GetIncidentRepairItems(long incidentId);

    Task<List<string>> GetLinkedRelatedIncidentInfo(long incidentId);

    Task<string> AddRelatedIncidentLink(long incidentId, long relatedIncidentId);

    Task<string> RemoveRelatedIncidentLink(long incidentId, long relatedIncidentId);

    Task<string> GetParentIncidentInfo(long incidentId);

    Task<string> AddParentIncidentLink(long incidentId, long parentIncidentId);

    Task<string> RemoveParentIncidentLink(long incidentId);

    Task<List<string>> GetChildIncidentsInfo(long incidentId);

    Task<string> GetParametersFromIncident(string incidentId, string instruction);

    Task<string> AddIncidentAttachmentFromFile(string incidentId, string filePath);

    Task<string> AddIncidentAttachmentFromContent(string incidentId, string fileName, string content);

    Task<List<Attachment>> ListIncidentAttachments(string incidentId);

    Task<List<ICMIncident>> ListIncidents(uint limit, uint offset, DateTime? since, string? owningServiceId, string? owningTeamId, string? incidentType, string? severity);

    //Task<string> DownloadIncidentAttachment(string incidentId, string attachmentId);
}
