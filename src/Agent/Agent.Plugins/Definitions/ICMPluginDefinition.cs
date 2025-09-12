using System.ComponentModel;
using Agent.Core.Models;
using Agent.Core.Models.ICM;
using Agent.Plugins.Interface;

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

    [Description("Get ICM incident details")]
    public async Task<Incident> GetIncidentInfo(
       [Description("Incident ID")] string incidentId)
    {
        return await _icmPlugin.GetIncidentInfo(incidentId);
    }


    [Description("Get ICM incident custom fields")]
    public async Task<List<CustomField>> GetCustomFields(
        [Description("Incident ID")] string incidentId)
    {
        return await _icmPlugin.GetCustomFields(incidentId);
    }


    [Description("Search for incidents and returns matching incidents with details like CreatedDateTime, Id, Title etc.")]
    public async Task<string> SearchIncidents(
        [Description("Search String")] string searchString,
        [Description("Lookback Period in Days")] int lookbackPeriodInDays,
        [Description("Limit on result count")] int resultCountLimit)
    {
        return await _icmPlugin.SearchIncidents(searchString, lookbackPeriodInDays, resultCountLimit);
    }


    [Description("Get current UTC date and time")]
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
    public async Task<DiscussionEntry?> GetAlertingDiscussionEntry(
        [Description("Incident ID")] string incidentId)
    {
        return await _icmPlugin.GetAlertingDiscussionEntry(incidentId);
    }


    [Description("Get ICM discussion entries")]
    public async Task<List<DiscussionEntry>> GetDiscussionEntries(
        [Description("Incident ID")] string incidentId)
    {
        return await _icmPlugin.GetDiscussionEntries(incidentId);
    }


    [Description("Transfer ICM incident")]
    public async Task<string> TransferIncident(
           [Description("Incident ID")] string incidentId,
           [Description("Discussion Entry - reason for transferring the incident")] string discussionEntry,
           [Description("Tenant ID of the team to transfer the incident to")] string tenantName,
           [Description("Team ID of the team to transfer the incident to")] string owningTeam)
    {
        return await _icmPlugin.TransferIncident(incidentId, discussionEntry, tenantName, owningTeam);
    }


    [Description("Mitigate ICM incident")]
    public async Task<string> MitigateIncident(
       [Description("Incident ID")] string incidentId,
       [Description("Discussion Entry (HTML) - reason for mitigating the incident")] string discussionEntry)
    {
        return await _icmPlugin.MitigateIncident(incidentId, discussionEntry);
    }


    [Description("Downgrade severity of ICM incident 2 to 3")]
    public async Task<string> DowngradeSeverity(
        [Description("Incident ID")] string incidentId,
        [Description("Discussion Entry (HTML) - reason for downgrading the incident")] string discussionEntry)
    {
        return await _icmPlugin.DowngradeSeverity(incidentId, discussionEntry);
    }


    [Description("Resolve ICM incident")]
    public async Task<string> ResolveIncident(
           [Description("Incident ID")] string incidentId,
           [Description("Discussion Entry (HTML) - reason for resolving the incident")] string discussionEntry)
    {
        return await _icmPlugin.ResolveIncident(incidentId, discussionEntry);
    }


    [Description("Post ICM discussion entry")]
    public async Task<string> PostDiscussionEntry(
       [Description("Incident ID")] string incidentId,
       [Description("Discussion Entry (HTML)")] string discussionEntry)
    {
        return await _icmPlugin.PostDiscussionEntry(incidentId, discussionEntry);
    }


    [Description("Add a tag to an ICM incident")]
    public async Task<string> AddTagToIncident(
        [Description("Id of the incident")] string incidentId,
        [Description("Tag to add")] string tag)
    {
        return await _icmPlugin.AddTagToIncident(incidentId, tag);
    }


    [Description("Add a keyword to an ICM incident")]
    public async Task<string> AddKeywordToIncident(
        [Description("Id of the incident")] string incidentId,
        [Description("Keyword to add")] string keyword)
    {
        return await _icmPlugin.AddKeywordToIncident(incidentId, keyword);
    }

    [Description("Acknowledges an ICM incident. Before Acknowledging validate if the incident is not already acknowledged, skip calling this tool if already acknowledged")]
    public async Task<string> AcknowledgeIncident(
        [Description("Incident ID")] string incidentId)
    {
        return await _icmPlugin.AcknowledgeIncident(incidentId);
    }


    [Description("Get repair items associated with an ICM incident")]
    public async Task<List<IncidentRepairItem>> GetIncidentRepairItems(
        [Description("Incident ID")] long incidentId)
    {
        return await _icmPlugin.GetIncidentRepairItems(incidentId);
    }


    [Description("​Gets basic info for all the linked incidents maked as related and associated with the given incident id")]
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
    public async Task<List<string>> GetChildIncidentsInfo(
        [Description("Incident ID used to fetch and return basic information about the child incidents associated with it.")] long incidentId)
    {
        return await _icmPlugin.GetChildIncidentsInfo(incidentId);
    }
}
