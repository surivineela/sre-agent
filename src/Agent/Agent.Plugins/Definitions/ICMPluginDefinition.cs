// ------------------------------------------------------------
// Copyright (c) Microsoft Corporation.  All rights reserved.
// ------------------------------------------------------------

using System.ComponentModel;
using System.Globalization;
using Agent.Core.Helpers;
using Agent.Core.Models;
using Agent.Framework;
using Agent.Plugins.Implementation;
using Agent.Plugins.Interface;
using Microsoft.AzureAd.Icm.IcmV3OData.Models;
using Microsoft.AzureAd.Icm.Types;
using Microsoft.SREAgent.Incidents.IcM.Model;
using Attachment = Microsoft.AzureAd.Icm.IcmV3OData.Models.Attachment;
using Incident = Microsoft.SREAgent.Incidents.IcM.Model.ICMIncident;

namespace Agent.Plugins.Definitions;

[AgentToolPlugin(
    IsFirstPartyOnly = true,
    Category = ToolCategories.IncidentManagement,
    IsIncidentHandlerPlugin = true,
    IncidentPlatform = Core.Configuration.IncidentManagementType.Icm)]
public class ICMPluginDefinition
{
    private readonly IICMPlugin _icmPlugin;

    public ICMPluginDefinition(IICMPlugin icmPlugin)
    {
        _icmPlugin = icmPlugin ?? throw new ArgumentNullException(nameof(icmPlugin));
    }

    private static readonly string[] KnownFormats =
    {
        "yyyy-MM-ddTHH:mm:ssZ",   // ISO 8601 UTC
        "yyyy-MM-ddTHH:mm:ss",    // ISO without Z
        "yyyy-MM-dd",             // Date only
        "MM/dd/yyyy HH:mm:ss",
        "MM/dd/yyyy",
        "dd/MM/yyyy",
        "dd-MMM-yyyy",
    };

    public static bool TryParseSmart(string? input, out DateTimeOffset result)
    {
        // First, try general parse
        if (DateTimeOffset.TryParse(input,
                                    CultureInfo.InvariantCulture,
                                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                    out result))
        {
            return true;
        }

        // Then, try known patterns
        foreach (var format in KnownFormats)
        {
            if (DateTimeOffset.TryParseExact(input,
                                             format,
                                             CultureInfo.InvariantCulture,
                                             DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                                             out result))
            {
                return true;
            }
        }
        return false;
    }

    [Description("Get ICM incident details")]
    [AgentTool(ToolMode.Auto)]
    public async Task<Incident> GetIncidentInfo(
       [Description("Incident ID")] long incidentId)
    {
        return await _icmPlugin.GetIncidentInfo(incidentId.ToString());
    }

    [Description("Get ICM incident custom fields")]
    [AgentTool(ToolMode.Auto)]
    public async Task<List<CustomField>> GetCustomFields(
        [Description("Incident ID")] long incidentId)
    {
        return await _icmPlugin.GetCustomFields(incidentId.ToString());
    }

    [Description("Search for incidents and returns matching incidents with details like CreatedDateTime, Id, Title etc.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> SearchIncidents(
        [Description("Keywords expected in incident title. Passing in just the incident id like 1234 will not produce any results.")] string searchString,
        [Description("Lookback Period in Days")] int lookbackPeriodInDays,
        [Description("Limit on result count")] int resultCountLimit)
    {
        return await _icmPlugin.SearchIncidents(searchString, lookbackPeriodInDays, resultCountLimit);
    }

    [Description("Get current UTC date and time")]
    [AgentTool(ToolMode.Auto)]
    public string GetCurrentUtcDateTime()
    {
        return _icmPlugin.GetCurrentUtcDateTime();
    }

    [Description("This tool identifies potential relationships between incidents. Invoke this tool whenever the user requests assistance with finding related, parent, or child incidents; especially when conditions such as time windows, title matching, or shared patterns are specified. The rules are applied internally to guide the agent's actions without being returned to the user.")]
    public string GetIcmCorrelationAndLinkingRules()
    {
        return _icmPlugin.GetIcmCorrelationAndLinkingRules();
    }

    [Description("Get Azure Alerting discussion entry")]
    [AgentTool(ToolMode.Auto)]
    public async Task<DescriptionEntry?> GetAlertingDiscussionEntry(
        [Description("Incident ID")] long incidentId)
    {
        return await _icmPlugin.GetAlertingDiscussionEntry(incidentId.ToString());
    }

    [Description("Get ICM discussion entries")]
    [AgentTool(ToolMode.Auto)]
    public async Task<List<DescriptionEntry>> GetDiscussionEntries(
        [Description("Incident ID")] long incidentId)
    {
        return await _icmPlugin.GetDiscussionEntries(incidentId.ToString());
    }

    [Description("Get top N ICM discussion entries")]
    [AgentTool(ToolMode.Auto)]
    public async Task<List<DescriptionEntry>> GetTopDiscussionEntries(
        [Description("Incident ID")] long incidentId,
        [Description("Number of top discussion entries to retrieve")] uint limit = 10,
        [Description("Set to true to get entries in ascending order of time, false for descending order")] bool IsAscending = true)
    {
        return await _icmPlugin.GetTopDiscussionEntries(incidentId.ToString(), limit, IsAscending);
    }

    [Description("Transfer ICM incident")]
    public async Task<string> TransferIncident(
           [Description("Incident ID")] long incidentId,
           [Description("Discussion Entry - reason for transferring the incident")] string discussionEntry,
           [Description("Tenant ID of the team to transfer the incident to")] string tenantName,
           [Description("Team ID of the team to transfer the incident to")] string owningTeam)
    {
        return await _icmPlugin.TransferIncident(incidentId.ToString(), discussionEntry, tenantName, owningTeam);
    }

    [Description("Mitigate ICM incident")]
    public async Task<string> MitigateIncident(
       [Description("Incident ID")] long incidentId,
       [Description("Discussion Entry (must be HTML, markdown is not allowed) - reason for mitigating the incident")] string discussionEntry)
    {
        return await _icmPlugin.MitigateIncident(incidentId.ToString(), discussionEntry);
    }

    [Description("Downgrade severity of ICM incident 2 to 3")]
    public async Task<string> DowngradeSeverity(
        [Description("Incident ID")] long incidentId,
        [Description("Discussion Entry (must be HTML, markdown is not allowed) - reason for downgrading the incident")] string discussionEntry)
    {
        return await _icmPlugin.DowngradeSeverity(incidentId.ToString(), discussionEntry);
    }

    //For the purposes of SRE Agent usage, we are only allowing SRE Agent to set severities 2 or lower.
    [Description("Update the severity level of an ICM incident to any level (1=Critical, 2=High, 3=Medium, 4=Low)")]
    public async Task<string> UpdateIncidentSeverity(
        [Description("Incident ID")] long incidentId,
        [Description("New severity level (2=Highest, 25 (reserved for Security Incidents), 3, 4=Lowest)")] int severity,
        [Description("Discussion Entry (must be HTML, markdown is not allowed) - reason for updating the incident severity")] string discussionEntry)
    {
        return await _icmPlugin.UpdateIncidentSeverity(incidentId.ToString(), severity, discussionEntry);
    }

    [Description("Resolve ICM incident")]
    public async Task<string> ResolveIncident(
           [Description("Incident ID")] long incidentId,
           [Description("Discussion Entry (must be HTML, markdown is not allowed) - reason for resolving the incident")] string discussionEntry)
    {
        return await _icmPlugin.ResolveIncident(incidentId.ToString(), discussionEntry);
    }

    [Description("Post an ICM discussion entry. IMPORTANT: The discussionEntry must be valid HTML only. Do NOT include any Markdown (no ``` fences, **bold**, # headings, lists, etc.). If you need formatting, use HTML tags.")]
    public async Task<string> PostDiscussionEntry(
       [Description("Incident ID")] long incidentId,
       [Description("Discussion Entry (Must be HTML only; Markdown is **not allowed**)")] string discussionEntry)
    {
        // Convert any chart-data blocks to base64 images for ICM
        var processedEntry = ChartHelper.ConvertChartDataBlocksToBase64Images(discussionEntry);
        return await _icmPlugin.PostDiscussionEntry(incidentId.ToString(), processedEntry);
    }

    [Description("Add a tag to an ICM incident")]
    public async Task<string> AddTagToIncident(
        [Description("Incident ID")] long incidentId,
        [Description("Tag to add")] string tag)
    {
        return await _icmPlugin.AddTagToIncident(incidentId.ToString(), tag);
    }

    [Description("Add a keyword to an ICM incident")]
    public async Task<string> AddKeywordToIncident(
        [Description("Incident ID")] long incidentId,
        [Description("Keyword to add")] string keyword)
    {
        return await _icmPlugin.AddKeywordToIncident(incidentId.ToString(), keyword);
    }

    [Description("Acknowledges an ICM incident. Before Acknowledging validate if the incident is not already acknowledged, skip calling this tool if already acknowledged")]
    public async Task<string> AcknowledgeIncident(
        [Description("Incident ID")] long incidentId)
    {
        return await _icmPlugin.AcknowledgeIncident(incidentId.ToString());
    }

    [Description("Get repair items associated with an ICM incident")]
    [AgentTool(ToolMode.Auto)]
    public async Task<List<ExternalLink>> GetIncidentRepairItems(
        [Description("Incident ID")] long incidentId)
    {
        return await _icmPlugin.GetIncidentRepairItems(incidentId);
    }

    [Description("​Gets basic info for all the linked incidents maked as related and associated with the given incident id")]
    [AgentTool(ToolMode.Auto)]
    public async Task<List<string>> GetLinkedRelatedIncidentInfo(
        [Description("Incident ID used to fetch and return basic information about the related incidents associated with it.")] long incidentId)
    {
        return await _icmPlugin.GetLinkedRelatedIncidentInfo(incidentId);
    }

    [Description("Adds a related incident link to the given incident id")]
    public async Task<string> AddRelatedIncidentLink(
        [Description("Incident ID to assign a related incident to")] long incidentId,
        [Description("Incident ID to assign as a related incident")] long relatedIncidentId)
    {
        return await _icmPlugin.AddRelatedIncidentLink(incidentId, relatedIncidentId);
    }

    [Description("Removes a related incident link from the given incident id")]
    public async Task<string> RemoveRelatedIncidentLink(
        [Description("Incident ID to remove the related incident from")] long incidentId,
        [Description("Incident ID to remove as a related incident")] long relatedIncidentId)
    {
        return await _icmPlugin.RemoveRelatedIncidentLink(incidentId, relatedIncidentId);
    }

    [Description("​Gets basic info of the parent incident associated with the given incident id")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> GetParentIncidentInfo(
        [Description("Incident ID used to fetch and return basic information about the parent incident ID associated with it.")] long incidentId)
    {
        return await _icmPlugin.GetParentIncidentInfo(incidentId);
    }

    [Description("Adds a parent incident link to the given incident id")]
    public async Task<string> AddParentIncidentLink(
        [Description("Incident ID to assign a parent to")] long incidentId,
        [Description("Incident ID to assign as a parent")] long parentIncidentId)
    {
        return await _icmPlugin.AddParentIncidentLink(incidentId, parentIncidentId);
    }

    [Description("Removes a parent incident link from the given incident id")]
    public async Task<string> RemoveParentIncidentLink(
        [Description("Incident ID to remove the parent from")] long incidentId)
    {
        return await _icmPlugin.RemoveParentIncidentLink(incidentId);
    }

    [Description("​Gets basic info for all the child incidents associated with the given incident id")]
    [AgentTool(ToolMode.Auto)]
    public async Task<List<string>> GetChildIncidentsInfo(
        [Description("Incident ID used to fetch and return basic information about the child incidents associated with it.")] long incidentId)
    {
        return await _icmPlugin.GetChildIncidentsInfo(incidentId);
    }

    [Description("Add a file attachment to an ICM incident by reading a file from the local filesystem")]
    public async Task<string> AddIncidentAttachmentFromFile(
        [Description("Incident ID")] long incidentId,
        [Description("Local file path to attach to the incident")] string filePath)
    {
        return await _icmPlugin.AddIncidentAttachmentFromFile(incidentId.ToString(), filePath);
    }

    [Description("Add a file attachment to an ICM incident from string content without requiring a local file")]
    public async Task<string> AddIncidentAttachmentFromContent(
        [Description("Incident ID")] long incidentId,
        [Description("Name of the file to create (with extension)")] string fileName,
        [Description("String content to attach as a file")] string content)
    {
        return await _icmPlugin.AddIncidentAttachmentFromContent(incidentId.ToString(), fileName, content);
    }

    [Description(@"""
        Purpose:
        Calculates the effective time range for issue investigation based on available timestamps.

        Scenario:
        Use this tool to determine the investigation window for an incident when at least one relevant timestamp is available.

        Output:
        Returns a JSON object with two fields:
        - StartDate (string): ISO 8601 timestamp of investigation start
        - EndDate (string): ISO 8601 timestamp of investigation end
        """
        )]
    [AgentTool(ToolMode.Auto)]
    public InvestigationTimeRangeResult GetIssueInvestigationTimeRange(
            [Description("ISO 8601 string for the first occurrence of the issue, or leave null if not available.")] string? issueFirstOccurrence,
            [Description("ISO 8601 string for the last occurrence of the issue, or leave null if not available.")] string? issueLastOccurrence,
            [Description("ISO 8601 string for when the issue was observed and reported, or leave null if not available.")] string? reportedIssueObservedOnTime)
    {
        TryParseSmart(issueFirstOccurrence, out var issueFirstOccurrenceDate);
        TryParseSmart(issueLastOccurrence, out var issueLastOccurrenceDate);
        TryParseSmart(reportedIssueObservedOnTime, out var reportedIssueObservedOnTimeDate);

        return _icmPlugin.GetIssueInvestigationTimeRange(issueFirstOccurrenceDate.DateTime, issueLastOccurrenceDate.DateTime, reportedIssueObservedOnTimeDate.DateTime);
    }

    [Description("List all attachments for an ICM incident")]
    [AgentTool(ToolMode.Auto)]
    public async Task<List<Attachment>> ListIncidentAttachments(
        [Description("Incident ID")] long incidentId)
    {
        return await _icmPlugin.ListIncidentAttachments(incidentId.ToString());
    }

    [Description("List ICM incidents with filtering and pagination support")]
    [AgentTool(ToolMode.Auto)]
    public async Task<List<Incident>> ListIncidents(
        [Description("Maximum number of incidents to return in a page")] int limit,
        [Description("Starting offset for pagination")] int offset,
        [Description("List incidents whose ModifiedDate is later than this datetime (ISO 8601 format). Do not specify time more then 100 days ago")] string? since,
        [Description("Filter by the owning service ID of the incident")] string? owningServiceId,
        [Description("Filter by the owning team ID of the incident")] string? owningTeamId,
        [Description("Filter by incident type. Must be provided if owner tean ID is provided. Valid values: 'CustomerReported', 'LiveSite', 'Deployment'")] string? incidentType,
        [Description("Filter by severity. Valid values: '0', '1', '2', '25', '3', '4'")] string? severity)
    {
        DateTime? sinceDate = null;
        if (!string.IsNullOrEmpty(since) && TryParseSmart(since, out var parsedDate))
        {
            sinceDate = parsedDate.DateTime;
        }

        return await _icmPlugin.ListIncidents(
            (uint)limit,
            (uint)offset,
            sinceDate,
            owningServiceId,
            owningTeamId,
            incidentType,
            severity);
    }

    //[Description("Download an attachment from an ICM incident. For text files (.txt, .log, .csv) under 1MB, returns content as string. Larger files or other types are saved locally.")]
    //public async Task<string> DownloadIncidentAttachment(
    //    [Description("Incident ID")] string incidentId,
    //    [Description("Attachment ID to download")] string attachmentId)
    //{
    //    return await _icmPlugin.DownloadIncidentAttachment(incidentId, attachmentId);
    //}

    [Description("Get incident details from IcM, using AI-enriched data when available. Set includeAlertDetails=false for a quick overview, or true for full context including the alerting Kusto query and results.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> GetIncidentDetails(
        [Description("Incident ID")] long incidentId,
        [Description("Set to true to include alerting details (Kusto query and results), false for quick overview")] bool includeAlertDetails = false)
    {
        return await _icmPlugin.GetIncidentDetails(incidentId.ToString(), includeAlertDetails);
    }

    [Description("Get the alerting entry that created the incident, including Kusto query and results.")]
    [AgentTool(ToolMode.Auto)]
    public async Task<string> GetIncidentAlertDetails(
        [Description("Incident ID")] long incidentId)
    {
        return await _icmPlugin.GetIncidentAlertDetails(incidentId.ToString());
    }
}
