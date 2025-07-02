using System.ComponentModel;
using Agent.Plugins;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Interface;
using Kusto.Cloud.Platform.Utils;
using Newtonsoft.Json;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppIcMPluginDefinition
    {
        private readonly IContainerAppIcMPlugin _plugin;

        public RCAContainerAppIcMPluginDefinition(IContainerAppIcMPlugin plugin)
        {
            _plugin = plugin;
        }

        [Description(@"
        Calculates the effective time range for issue investigation based on the available input parameters. 
        At least one of the following must be provided: issueFirstOccurrence, issueLastOccurrence, or reportedIssueObservedOnTime.
        **Important:**
        - Do NOT use this function if none of the input parameters are available.
        ")]
        public (DateTime StartDate, DateTime EndDate) GetIssueInvestigationTimeRangeRCAContainerApp(
            [Description("ISO 8601 date format string of first occurrence of the issue. Skip if not available")] string? issueFirstOccurrence,
            [Description("ISO 8601 date format string of the last occurrence of the issue. Skip if not available")] string? issueLastOccurrence,
            [Description("ISO 8601 date format string  when the issue was observed and reported. Skip if not available")] string? reportedIssueObservedOnTime)
        {
            var issueFirstOccurrenceDate = issueFirstOccurrence.IsNotNullOrEmpty() ? DateTime.Parse(issueFirstOccurrence!) : (DateTime?)null;
            var issueLastOccurrenceDate = issueLastOccurrence.IsNotNullOrEmpty() ? DateTime.Parse(issueLastOccurrence!) : (DateTime?)null;
            var reportedIssueObservedOnTimeDate = reportedIssueObservedOnTime.IsNotNullOrEmpty() ? DateTime.Parse(reportedIssueObservedOnTime!) : (DateTime?)null;

            return _plugin.GetIssueInvestigationTimeRange(issueFirstOccurrenceDate, issueLastOccurrenceDate, reportedIssueObservedOnTimeDate);
        }

        [Description(@"Get base ICM incident information.
            Returns a JSON-formatted string containing:
           - IncidentId
           - Creation and last update time
           - Status
           - Severity level
           - Summary")]
        public async Task<string?> GetIncidentInfoRCAContainerApp(
        [Description("Incident ID")] string incidentId)
        {
            var incident = await _plugin.GetIncidentInfo(incidentId);
            var incidentString = JsonConvert.SerializeObject(incident);
            return incidentString;
        }

        //[Description(@"Get original ICM discussion entries
        //This operation will get all the discussion entries of the given IcM Incident.
        //Input parameters:
        //- IncidentId: The Id of the IcM incident. It is usually a integer number.
        //- QueryFrom: The timestamp for filter the discussion entries which are created after it.
        //The return value is a list of discussion entries of the given IcM Incident. Each discussion entry includes the following information:
        //- IncidentId: The Id of the IcM incident.
        //- TimeStamp: The timestamp of the discussion entry.
        //- ChangedBy: The user who created this discussion entry.
        //")]
        //public async Task<string?> GetDiscussionEntriesRCAContainerApp(
        //   [Description("Incident ID")] string incidentId,
        //   [Description("From time of the query")] DateTimeOffset queryFrom)
        //{
        //    var discussionEntries = await _plugin.GetDiscussionEntries(incidentId, queryFrom);
        //    var discussionEntriesString = JsonConvert.SerializeObject(discussionEntries, Formatting.Indented);
        //    return discussionEntriesString;
        //}


        [Description(@"Provide official RCA from container apps template
        This operation will take the one liner RCA and use the below template to provide a official formatted RCA.
        - oneLinerRCA: The one liner RCA that needs to be formatted into the RCA template.
        ")]
        public string OneLinerToRCA(
         [Description("This is the one liner RCA that needs to be formatted into the RCA template")] string oneLinerRCA)
        {
            return _plugin.OneLinerToRCA(oneLinerRCA);
        }

        [Description(@"
        Submit feedback regarding the agent's assistance in debugging the issue.
        clearly give both choices 'was agent helpful?' and 'is resolution accurate or close?'
        Input parameters:
        - IncidentId: The unique identifier of the incident.
        - wasHelpful: Indicates whether the agent was helpful in debugging the issue (true/false). Use null to skip this feedback.
        - isResolutionCorrect: Indicates whether the resolution provided by the agent was accurate (true/false). Use null to skip this feedback.
        ")]
        public async void WasAgentHelpfulInDebuggingIssueAsync(
           [Description("The unique identifier of the incident.")] string incidentId,
           [Description("Indicates if the agent was helpful in debugging the issue (true/false). Use null to skip.")] bool? wasHelpful,
           [Description("Indicates if the resolution provided by the agent was accurate (true/false). Use null to skip.")] bool? isResolutionCorrect)
        {
            await _plugin.WasAgentHelpfulInDebuggingIssueAsync(incidentId, wasHelpful, isResolutionCorrect);
        }

        [Description(@"
        **Note: DO NOT CALL IT AUTOMATICALLY. ALWAYS ASK USER BEFORE CALLING IT**
        Add a valid HTML-formatted message discussion entry or summary of final investigate to an ICM incident
        This operation will add a discussion entry to the given IcM Incident. 
        input parameters:
        - incidentId: The Id of the IcM incident. It is usually a integer number.
        - text: A well HTML-formatted message to add as discussion to IcM.
        NOTE:
            - text MUST be always valid HTML formatted message
            - Remove all emojis if any present. 
        The operation will add a discussion entry to the given incident.
        The return value is a boolean value for indicating if the operation is successful.
        ")]
        public async Task<bool> AddDiscussionEntryRCAContainerApp(
        [Description("Incident ID")] string incidentId,
        [Description("Discussion entry text")] string text)
        {
            return await _plugin.AddDiscussionEntry(incidentId, text);
        }

        [Description(@"Resolve an ICM incident. This operation will set the given IcM Incident to Resolved state. And you must give a reason of this resolve action.
        **Note: Always confirm with the user before resolving the ICM incident, or proceed only if the user has already provided confirmation**

        Input parameters:
        - incidentId: The Id of the IcM incident.It is usually a integer number.
        - reason: Usually it is a reason why you can resolve this incident.
        The operation will mark the given incident as resolved. The return value is a boolean value for indicating if the operation is successful.
        ")]
        public async Task<bool> ResolveIncidentRCAContainerApp(
        [Description("Incident ID")] string incidentId,
        [Description("comment/reason for resolution action")] string reason)
        {
            return await _plugin.ResolveIncident(incidentId, reason);
        }

    }
}
