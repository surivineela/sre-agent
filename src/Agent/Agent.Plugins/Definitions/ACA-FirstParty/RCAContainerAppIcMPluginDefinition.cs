using System.ComponentModel;
using Agent.Core;
using Agent.Plugins;
using Agent.Plugins.IcmPlugin;
using Agent.Plugins.Interface;
using FirstPartyAgent.Common.Configuration;
using Kusto.Cloud.Platform.Utils;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using ReverseMarkdown.Converters;
using ScottPlot.Palettes;

namespace Agent.Plugins.Definitions
{
    [AgentToolPlugin(IsFirstPartyOnly = true)]
    public class RCAContainerAppIcMPluginDefinition
    {
        private readonly IContainerAppIcMPlugin _plugin;
        private readonly IWebHostEnvironment _env;
        private readonly GeneralSettings _generalSettings;
        private readonly string icmWebPortalMessage = @"Please use this link to view the SRE agent investigation in the web portal: <a href = ""{0}"" > Azure portal link</a>";

        public RCAContainerAppIcMPluginDefinition(IContainerAppIcMPlugin plugin, IWebHostEnvironment env, IOptions<GeneralSettings> generalSettings)
        {
            _plugin = plugin;
            _env = env;
            _generalSettings = generalSettings.Value ?? new GeneralSettings();
        }

        [Description(@"""
        Purpose:
        Calculates the effective time range for issue investigation based on available timestamps.

        Scenario:
        Use this tool to determine the investigation window for an incident when at least one relevant timestamp is available.

        Output:
        Returns a tuple with two values:
        - StartDate: The calculated start date of the investigation window
        - EndDate: The calculated end date of the investigation window
        """
        )]
        public (DateTime StartDate, DateTime EndDate) GetIssueInvestigationTimeRangeRCAContainerApp(
            [Description("ISO 8601 string for the first occurrence of the issue, or leave null if not available.")] string? issueFirstOccurrence,
            [Description("ISO 8601 string for the last occurrence of the issue, or leave null if not available.")] string? issueLastOccurrence,
            [Description("ISO 8601 string for when the issue was observed and reported, or leave null if not available.")] string? reportedIssueObservedOnTime)
        {
            var issueFirstOccurrenceDate = issueFirstOccurrence.IsNotNullOrEmpty() ? DateTime.Parse(issueFirstOccurrence!) : (DateTime?)null;
            var issueLastOccurrenceDate = issueLastOccurrence.IsNotNullOrEmpty() ? DateTime.Parse(issueLastOccurrence!) : (DateTime?)null;
            var reportedIssueObservedOnTimeDate = reportedIssueObservedOnTime.IsNotNullOrEmpty() ? DateTime.Parse(reportedIssueObservedOnTime!) : (DateTime?)null;

            return _plugin.GetIssueInvestigationTimeRange(issueFirstOccurrenceDate, issueLastOccurrenceDate, reportedIssueObservedOnTimeDate);
        }

        [Description(@"""
        Purpose:
        Retrieves detailed information about a specific ICM incident.

        Scenario:
        Use this tool to get all available data for a given incident ID.

        Output:
        Returns a JSON object containing incident details such as IncidentId, creation and last update time, status, severity level, and summary.
        """
        )]
        public async Task<string?> GetIncidentInfoRCAContainerApp(
            [Description("Unique identifier for the ICM incident.")] string incidentId)
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

        [Description(@"""
        Purpose:
        Formats a one-liner RCA statement into an official RCA template.

        Scenario:
        Use this tool to generate a formal RCA document from a brief summary.

        Output:
        Returns a string containing the formatted RCA:
        - RCA: Officially formatted RCA text
        """
        )]
        public string OneLinerToRCA(
            [Description("One-liner RCA statement to be formatted.")] string oneLinerRCA)
        {
            return _plugin.OneLinerToRCA(oneLinerRCA);
        }

        [Description(@"""
        Purpose:
        Submits feedback about the agent's assistance in debugging an incident.

        Scenario:
        Use this tool to record if the agent was helpful and if the resolution was accurate.

        Output:
        No return value.
        """
        )]
        public async void WasAgentHelpfulInDebuggingIssueAsync(
            [Description("Unique identifier for the ICM incident.")] string incidentId,
            [Description("Set to true if the agent was helpful, false if not, or null to skip.")] bool? wasHelpful,
            [Description("Set to true if the resolution was accurate, false if not, or null to skip.")] bool? isResolutionCorrect)
        {
            await _plugin.WasAgentHelpfulInDebuggingIssueAsync(incidentId, wasHelpful, isResolutionCorrect);
        }

        [Description(@"""
        Purpose:
        Adds a new HTML-formatted discussion entry to an ICM incident.

        Scenario:
        Use this tool to post a summary or message to the incident's discussion log.

        Output:
        Returns a boolean value indicating if the operation was successful:
        - Success: true if the entry was added, false otherwise
        """
        )]
        public async Task<bool> AddDiscussionEntryRCAContainerApp(
            [Description("Unique identifier for the ICM incident.")] string incidentId,
            [Description("HTML-formatted discussion entry text to add.")] string text)
        {
            return await _plugin.AddDiscussionEntry(incidentId, text);
        }

        [Description(@"""
        Purpose:
        Adds a new HTML-formatted discussion entry to an ICM incident containing a link to the SRE agent investigation in the Azure portal.

        Scenario:
        Use this tool to post a web portal link to the incident's discussion log for easy access to the investigation thread.

        Output:
        Returns a boolean value indicating if the operation was successful:
        - Success: true if the entry was added (or the entry already exists), false otherwise
        """
        )]
        public async Task<bool> AddWebPortalLinkToIncidentRCAContainerApp(
            [Description("Unique identifier for the ICM incident.")] string incidentId,
            [Description("Date and time of the ICM creation.")] DateTime icmCreateTime)
        {
            var templateUrl = _generalSettings.PortalThreadIdLink;
            if (_env.IsDevelopment())
            {
                return true; // Do not add web portal link to ICM in development environment
            }
            var currentThreadId = ToolStatic.AsyncLocalThreadId.Value;
            var threadLink = string.Format(templateUrl, currentThreadId);
            var webPortalLink = string.Format(icmWebPortalMessage, threadLink);

            // Check if the web portal link already exists in the discussion entries
            var existingDiscussionEntries = await _plugin.GetDiscussionEntries(incidentId, icmCreateTime);
            bool webPortalLinkExists = existingDiscussionEntries != null &&
                existingDiscussionEntries.Any(entry => entry.Text != null && entry.Text.Contains(currentThreadId.ToString()));
            if (webPortalLinkExists)
            {
                return true; // Web portal link already exists, no need to add again
            }
                return await _plugin.AddDiscussionEntry(incidentId, webPortalLink);
        }

        [Description(@"""
        Purpose:
        Resolves an ICM incident and sets its status to resolved with a provided reason.

        Scenario:
        Use this tool to mark an incident as resolved after confirmation.

        Output:
        Returns a boolean value indicating if the operation was successful:
        - Success: true if the incident was resolved, false otherwise
        """
        )]
        public async Task<bool> ResolveIncidentRCAContainerApp(
            [Description("Unique identifier for the ICM incident.")] string incidentId,
            [Description("Reason or comment for resolving the incident.")] string reason)
        {
            return await _plugin.ResolveIncident(incidentId, reason);
        }

    }
}
