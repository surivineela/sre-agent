// ------------------------------------------------------------
//  Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Agent.Core.Models.ICM;
using Agent.Logging;
using Agent.Plugins.Interface;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using OpenAI.Chat;
using ChatResponseFormat = Microsoft.Extensions.AI.ChatResponseFormat;

namespace Agent.Plugins.IcmPlugin;
public class ContainerAppIcMPlugin : IContainerAppIcMPlugin
{
    private readonly ILogger<ContainerAppIcMPlugin> _logger;
    private readonly IChatClient _chatClient;
    private readonly ITimePlugin _timePlugin;
    internal readonly ICMWorkflowClient _icmAutomationClient;

    public ContainerAppIcMPlugin(
            IConfiguration config,
            ICMWorkflowClient icmAutomationClient,
            IChatClient chatClient,
            ITimePlugin timePlugin,
            ILogger<ContainerAppIcMPlugin> logger)
    {
        _logger = logger;
        _chatClient = chatClient;
        _timePlugin = timePlugin;
        _icmAutomationClient = icmAutomationClient;
    }

    public (DateTime StartDate, DateTime EndDate) GetIssueInvestigationTimeRange(DateTime? issueFirstOccurence, DateTime? issueLastOccurene, DateTime? reportedIssueObservedOnTime)
    {
        if (issueFirstOccurence == null && issueLastOccurene == null && reportedIssueObservedOnTime == null)
        {
            throw new ArgumentException("At least one of the issueFirstOccurence, issueLastOccurene or reportedIssueObservedOnTime should be provided.");
        }

        var now = DateTime.UtcNow;

        // If no endDate, set to now
        DateTime endDate = issueLastOccurene
            ?? (reportedIssueObservedOnTime.HasValue ? reportedIssueObservedOnTime.Value.AddDays(2) : now);

        // If no startDate, set to now-10d
        DateTime startDate = issueFirstOccurence
            ?? (reportedIssueObservedOnTime.HasValue ? reportedIssueObservedOnTime.Value.AddDays(-2) : now.AddDays(-10));

        // Ensure the start date is not after the end date
        if (startDate > endDate)
        {
            startDate = endDate.AddDays(-10);
        }

        // If the range is greater than 1 month, adjust startDate to be 1 month before endDate
        if ((endDate - startDate).TotalDays > 30)
        {
            startDate = endDate.AddMonths(-1);
        }

        // If end date is older than 4 months, throw error
        if ((now - endDate).TotalDays > 120)
        {
            throw new ArgumentException("Issue end date is older than 4 months. Please specify correct dates as we can't investigate it.");
        }

        // Add a 1-hour buffer before and after the time window to capture events near the start and end of the investigation period.
        startDate = startDate.AddHours(-1);
        endDate = endDate.AddHours(1);

        _logger.LogInternalInformation($"Calculated investigation time range: StartDate={startDate}, EndDate={endDate}");
        return (startDate, endDate);
    }

    public async Task<Incident?> GetIncidentInfo(string incidentId)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        Incident? incident = await _icmAutomationClient.GetIncidentAsync(incidentId);
        if (incident != null)
        {
            incident.DiscussionEntry = RemoveImageTags(incident.DiscussionEntry);
            incident.Summary = RemoveImageTags(incident.Summary);
        }
        stopwatch.Stop();
        _logger.LogExternalInformation($"Fetched raw ICM incident details for ICM ID {incidentId} total time took in fetching: {(int)stopwatch.ElapsedMilliseconds} msecs");
        return incident;
    }

    public async Task<List<DiscussionEntry>?> GetDiscussionEntries(
        string incidentId,
        DateTimeOffset queryFrom)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        List<DiscussionEntry> discussionEntries = await _icmAutomationClient.GetIncidentDiscussionEntriesAsync(incidentId, queryFrom) ?? new List<DiscussionEntry>();
        foreach (var discussionEntry in discussionEntries)
        {
            if (discussionEntry.Text != null && discussionEntry.IsHtml)
            {
                discussionEntry.Text = RemoveImageTags(discussionEntry.Text);
            }
        }
        stopwatch.Stop();
        _logger.LogExternalInformation($"Fetched raw ICM discussion enteries for ICM ID {incidentId} total time took in fetching: {(int)stopwatch.ElapsedMilliseconds} msecs");
        return discussionEntries;
    }

    public string OneLinerToRCA(
        string oneLinerRCA)
    {
        string template = "The Microsoft Azure Team has investigated the issue you reported related to describe the symptom of the issue#\r\n\r\nThis issue was found to be related to a bug in describe the root cause of the issue. We developed a fix and deployed the changes to production. This specific environment will be/was updated on replace with the target date/week/month#\r\n\r\nWe are continuously taking steps to improve the service and our processes to ensure such incidents do not occur in the future, and in this case it includes (but is not limited to):\r\n\r\nlist out the possible repair item\r\n\r\nWe apologize for any inconvenience.\r\n\r\nRegards,\r\n\r\nThe Microsoft Azure Team";
        return $"Container Apps RCA template {template} + One Liner RCA: {oneLinerRCA}";
    }

    private static string RemoveImageTags(string? html)
    {
        if (string.IsNullOrEmpty(html))
        {
            return string.Empty;
        }
        // Regex to match <img ...> tags (case-insensitive)
        string pattern = @"<img[^>]*>";
        return Regex.Replace(html, pattern, string.Empty, RegexOptions.IgnoreCase);
    }

    public async Task WasAgentHelpfulInDebuggingIssueAsync(string incidentId, bool? wasHelpful, bool? isResolutionCorrect)
    {
        if (wasHelpful != null)
        {
            await AddTag(incidentId, wasHelpful == true ? "AgentHelpful:true" : "AgentHelpful:false");
        }
        if (isResolutionCorrect != null)
        {
            await AddTag(incidentId, isResolutionCorrect == true ? "AgentResolutionCorrect:true" : "AgentResolutionCorrect:false");
        }
    }

    public async Task<bool> AddTag(
            [Description("Incident ID")] string incidentId,
            [Description("Tag to add")] string tag)
    {
        return await _icmAutomationClient.AddTagToIncident(incidentId, tag) == "Success";
    }

    public async Task<bool> MitigateIncident(
        [Description("Id of the incident")] string incidentId,
        [Description("The comment for mitigation action")] string reason)
    {
        return await _icmAutomationClient.MitigateIncidentAsync(incidentId, reason) == "Success";
    }

    public async Task<bool> ResolveIncident(
            [Description("Id of the incident")] string incidentId,
            [Description("comment/reason for resolution action")] string reason)
    {
        return await _icmAutomationClient.ResolveIncidentAsync(incidentId, reason) == "Success";
    }

    public async Task<bool> AddDiscussionEntry(
            [Description("Incident ID")] string incidentId,
            [Description("Discussion entry text")] string text)
    {
        return await _icmAutomationClient.PostDiscussionEntryAsync(incidentId, text) == "Success";
    }
}
