using Agent.Plugins.Definitions;
using Microsoft.AzureAd.Icm.Types;
using Microsoft.SREAgent.Incidents.IcM.Model;

namespace Agent.Plugins.Interface;
public interface IContainerAppIcMPlugin
{
    InvestigationTimeRangeResult GetIssueInvestigationTimeRange(DateTime? issueFirstOccurence, DateTime? issueLastOccurene, DateTime? reportedIssueObservedOnTime);
    Task WasAgentHelpfulInDebuggingIssueAsync(string incidentId, bool? wasHelpful, bool? isResolutionCorrect);
    Task<ICMIncident?> GetIncidentInfo(string incidentId);
    Task<string> MitigateIncident(string incidentId, string reason);
    Task<string> ResolveIncident(string incidentId, string reason);
    Task<List<DescriptionEntry>?> GetDiscussionEntries(string incidentId, DateTimeOffset queryFrom);
    Task<string> AddDiscussionEntry(string incidentId, string text);
    Task<bool> AddTag(string incidentId, string tag);
    string OneLinerToRCA(string oneLinerRCA);
}
