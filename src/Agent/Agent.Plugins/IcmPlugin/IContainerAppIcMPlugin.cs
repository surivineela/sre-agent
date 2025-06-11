using Agent.Plugins.IcmPlugin;
using Microsoft.Extensions.AI;
using Microsoft.Graph.Models.Security;

namespace Agent.Plugins.IcmPlugin;
public interface IContainerAppIcMPlugin
{
    (DateTime StartDate, DateTime EndDate) GetIssueInvestigationTimeRange(DateTime? issueFirstOccurence, DateTime? issueLastOccurene, DateTime? reportedIssueObservedOnTime);
    Task WasAgentHelpfulInDebuggingIssueAsync(string incidentId, bool? wasHelpful, bool? isResolutionCorrect);
    Task<Incident?> GetIncidentInfo(string incidentId);
    Task<bool> MitigateIncident(string incidentId, string reason);
    Task<bool> ResolveIncident(string incidentId, string reason);
    Task<List<DiscussionEntry>?> GetDiscussionEntries(string incidentId, DateTimeOffset queryFrom);
    Task<bool> AddDiscussionEntry(string incidentId, string text);
    Task<bool> AddTag(string incidentId, string tag);
    string OneLinerToRCA(string oneLinerRCA);
}
