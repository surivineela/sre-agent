using Agent.Core.Configuration;
using Microsoft.AzureAd.Icm.IcmV3OData.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.AzureAd.Icm.Types;
using Microsoft.SREAgent.Incidents.IcM.Model;
using System.Text.Json;
using IICMAPIClientSDKClient = Microsoft.SREAgent.Incidents.IcM.Interface.IICMAPIClient;
using Incident = Microsoft.SREAgent.Incidents.IcM.Model.ICMIncident;
using Attachment = Microsoft.AzureAd.Icm.IcmV3OData.Models.Attachment;
using IncidentStatus = Microsoft.AzureAd.Icm.Types.IncidentStatus;




namespace Agent.Core.Services;
public interface IICMAPIClient
{
    Task<Incident> GetIncidentAsync(string incidentId);
    Task<List<Incident>> GetIncidentsAsync(uint limit, uint offset, DateTime? lastModifiedDate, string? owningServiceId, string? titleContains, string? owningTeamId = null, string? incidentType = null, string? createdBy = null, string? monitorId = null, string? severity = null, IEnumerable<string>? statuses = null);
    Task<List<CustomField>> GetCustomFieldsAsync(string incidentId);
    Task<List<Incident>> SearchIncidentsAsync(string searchString);
    Task<List<DescriptionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId);
    Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantId, string teamId);
    Task<string> ChangeSeverityAsync(string incidentId, int severity, string discussionEntry, bool htmlRendering = true);
    Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string howFixed = "", string mitigateContactAlias = "");
    Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string resolveContactAlias = "");
    Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry, bool htmlRendering = true);
    Task<string> SetIncidentTags(string incidentId, List<string> tags);
    Task<string> AddTagToIncident(string incidentId, string tag);
    Task<string> AddKeywordToIncident(string incidentId, string keyword);
    Task<string> AcknowledgeIncidentAsync(string incidentId, string acknowledgeContactAlias = "");
    Task<List<string>> GetLinkedRelatedIncidentInfoAsync(long incidentId);
    Task<string> AddRelatedIncidentLinkAsync(long incidentId, long relatedIncidentId);
    Task<string> RemoveRelatedIncidentLinkAsync(long incidentId, long relatedIncidentId);
    Task<string> GetParentIncidentInfoAsync(long incidentId);
    Task<string> AddParentIncidentLinkAsync(long incidentId, long parentIncidentId);
    Task<string> RemoveParentIncidentLinkAsync(long incidentId);
    Task<List<string>> GetChildIncidentsInfoAsync(long incidentId);
    Task<List<ExternalLink>> GetIncidentRepairItemsAsync(long incidentId);
    Task<string> AddIncidentAttachment(string incidentId, string fileName, string base64Content);
    Task<List<Attachment>> ListIncidentAttachments(string incidentId);
    //Task<string> DownloadIncidentAttachment(string incidentId, string attachmentId);
}

public class ICMAPIClient : IICMAPIClient
{
    private readonly ICMAPISettings _icmApiSettings;
    private IncidentManagementSettings _current;
    private readonly ILogger<ICMAPIClient> _logger;
    private readonly IICMAPIClientSDKClient _icmApiClientSDKService;

    public ICMAPIClient(ILogger<ICMAPIClient> logger, IICMAPIClientSDKClient iCMAPIClientSDKService, IOptionsMonitor<IncidentManagementSettings> monitor)
    {
        _logger = logger;
        _icmApiClientSDKService = iCMAPIClientSDKService;
        _current = monitor.CurrentValue;
        monitor.OnChange(newConfig =>
        {
            _current = newConfig;
        });
        _icmApiSettings = _current.ICMAPI;
    }


    public async Task<Incident> GetIncidentAsync(string incidentId)
    {
        try
        {
            return await _icmApiClientSDKService.GetIncidentAsync(incidentId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to retrieve incident :{incidentId}.");
            throw;
        }
    }

    public async Task<List<Incident>> GetIncidentsAsync(uint limit, uint offset, DateTime? lastModifiedDate, string? owningServiceId, string? titleContains, string? owningTeamId = null, string? incidentType = null, string? createdBy = null, string? monitorId = null, string? severity = null, IEnumerable<string>? statuses = null)
    {
        owningServiceId = owningServiceId ?? _icmApiSettings.OwningServiceId;
        return await _icmApiClientSDKService.GetIncidentsAsync(
            limit: limit,
            offset: offset,
            lastModifiedDate: lastModifiedDate,
            owningServiceId: owningServiceId,
            titleContains: titleContains,
            owningTeamId: owningTeamId,
            incidentType: incidentType,
            createdBy: createdBy,
            monitorId: monitorId,
            severity: severity,
            statuses: statuses
        );
    }

    public async Task<List<CustomField>> GetCustomFieldsAsync(string incidentId)
    {
        return await _icmApiClientSDKService.GetCustomFieldsAsync(incidentId);
    }

    public async Task<List<DescriptionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId)
    {
        try
        {
            return await _icmApiClientSDKService.GetIncidentDiscussionEntriesAsync(incidentId);
        }
        catch (Exception ex)
        {
            _logger.LogInternalError(ex, $"Failed to retrieve incident discussion entries for incident :{incidentId}.");
            throw;
        }
    }

    //ICM should be only in ACTIVE/MITIGATE state can transfer
    public async Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantId, string teamId)
    {
        var incident = await GetIncidentAsync(incidentId);
        var allowedStates = new[] { IncidentStatus.Active, IncidentStatus.Mitigated };
        if (!CheckIncidentState(incident, allowedStates))
        {
            throw new InvalidOperationException($"Incident {incidentId} with state {incident.State} is not in a valid state to transfer. Only {string.Join(", ", allowedStates)} are allowed.");
        }

        var res = await _icmApiClientSDKService.TransferIncidentAsync(incidentId, discussionEntry, tenantId, teamId);
        if (!res.Success)
        {
            throw new Exception($"Failed to transfer incident. Error: {res.Message}");
        }
        return res.Message;
    }

    public async Task<string> ChangeSeverityAsync(string incidentId, int severity, string discussionEntry, bool htmlRendering = true)
    {
        var res = await _icmApiClientSDKService.ChangeSeverityAsync(incidentId, severity, discussionEntry, htmlRendering);
        if (!res.Success)
        {
            throw new Exception($"Failed to change severity. Error: {res.Message}");
        }
        return res.Message;
    }

    //Only active incident can be mitigated
    //If is resolved, no more further action needed
    public async Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string howFixed = "", string mitigateContactAlias = "")
    {

        var incident = await GetIncidentAsync(incidentId);

        if (CheckIncidentState(incident, new[] { IncidentStatus.Resolved }))
        {
            return $"Incident {incidentId} is already in Resolved state.";
        }

        var allowedStates = new[] { IncidentStatus.Active };
        if (!CheckIncidentState(incident, allowedStates))
        {
            throw new InvalidOperationException($"Incident {incidentId} with state {incident.State} is not in a valid state to mitigate. Only {string.Join(", ", allowedStates)} are allowed.");
        }
        var res = await _icmApiClientSDKService.MitigateIncidentAsync(incidentId, discussionEntry, isCustomerImpacting, isNoise, howFixed, mitigateContactAlias);
        if (!res.Success)
        {
            throw new Exception($"Failed to mitigate incident. Error: {res.Message}");
        }
        return res.Message;
    }

    public async Task<List<Incident>> SearchIncidentsAsync(string searchString)
    {
        var res = await _icmApiClientSDKService.SearchIncidentsAsync(searchString);
        return res.Incidents.ToList();
    }

    //Only mitigated incident can be resolved
    public async Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string resolveContactAlias = "")
    {
        var incident = await GetIncidentAsync(incidentId);
        if (CheckIncidentState(incident, new[] { IncidentStatus.Active }))
        {
            await MitigateIncidentAsync(incidentId, $"Mitigate from {incident.State} to {IncidentStatus.Mitigated} for resolving");
            incident = await GetIncidentAsync(incidentId);
        }

        var allowedStates = new[] { IncidentStatus.Mitigated };
        if (!CheckIncidentState(incident, allowedStates))
        {
            throw new InvalidOperationException($"Incident {incidentId} with state {incident.State} is not in a valid state to resolve. Only {string.Join(", ", allowedStates)} are allowed.");
        }

        var res = await _icmApiClientSDKService.ResolveIncidentAsync(incidentId, discussionEntry, isCustomerImpacting, isNoise, resolveContactAlias);
        if (!res.Success)
        {
            throw new Exception($"Failed to resolve incident. Error: {res.Message}");
        }
        return res.Message;
    }

    public async Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry, bool htmlRendering = true)
    {
        var res = await _icmApiClientSDKService.PostDiscussionEntryAsync(incidentId, discussionEntry, htmlRendering);
        if (!res.Success)
        {
            throw new Exception($"Failed to post discussion entry. Error: {res.Message}");
        }
        return res.Message;
    }

    public async Task<string> SetIncidentTags(string incidentId, List<string> tags)
    {
        var res = await _icmApiClientSDKService.SetIncidentTags(incidentId, tags);
        if (!res.Success)
        {
            throw new Exception($"Failed to set incident tags. Error: {res.Message}");
        }
        return res.Message;
    }

    public async Task<string> AddTagToIncident(string incidentId, string tag)
    {
        var res = await _icmApiClientSDKService.AddTagToIncident(incidentId, tag);
        if (!res.Success)
        {
            throw new Exception($"Failed to add tag to incident. Error: {res.Message}");
        }
        return res.Message;
    }

    public async Task<string> AddKeywordToIncident(string incidentId, string keyword)
    {
        var res = await _icmApiClientSDKService.AddKeywordToIncident(incidentId, keyword);
        if (!res.Success)
        {
            throw new Exception($"Failed to add keyword to incident. Error: {res.Message}");
        }
        return res.Message;
    }
    //If is resovled, cannot be ack
    public async Task<string> AcknowledgeIncidentAsync(string incidentId, string acknowledgeContactAlias = "")
    {
        var res = await _icmApiClientSDKService.AcknowledgeIncidentAsync(incidentId, acknowledgeContactAlias);
        if (!res.Success)
        {
            throw new Exception($"Failed to acknowledge incident. Error: {res.Message}");
        }
        return res.Message;
    }

    public async Task<string> AddIncidentAttachment(string incidentId, string fileName, string base64Content)
    {
        var res = await _icmApiClientSDKService.AddIncidentAttachment(incidentId, fileName, base64Content);
        if (!res.Success)
        {
            throw new Exception($"Failed to add incident attachment. Error: {res.Message}");
        }
        return res.Message;
    }

    #region RelatedIncident CRD
    public async Task<List<string>> GetLinkedRelatedIncidentInfoAsync(long incidentId)
    {
        var linkedIncidents = await _icmApiClientSDKService.GetLinkedRelatedIncidentsInfoAsync(incidentId.ToString());
        return linkedIncidents.Any() ?
                    linkedIncidents.Select(incident => JsonSerializer.Serialize(incident)).ToList()
                    : new List<string>() { $"Cannnot found any linked related incidents with {incidentId}" };
    }

    public async Task<string> AddRelatedIncidentLinkAsync(long incidentId, long relatedIncidentId)
    {
        var res = await _icmApiClientSDKService.AddRelatedIncidentLinkAsync(incidentId.ToString(), relatedIncidentId.ToString());
        if (!res.Success)
        {
            throw new Exception($"Failed to link related incident {incidentId} with {relatedIncidentId}. Error: {res.Message}");
        }
        return res.Message;
    }

    public async Task<string> RemoveRelatedIncidentLinkAsync(long incidentId, long relatedIncidentId)
    {
        var res = await _icmApiClientSDKService.RemoveRelatedIncidentLinkAsync(incidentId.ToString(), relatedIncidentId.ToString());
        if (!res.Success)
        {
            throw new Exception($"Failed to unlink related incident {incidentId} with {relatedIncidentId}. Error: {res.Message}");
        }
        return res.Message;
    }
    #endregion

    #region ParentIncident CRD
    public async Task<string> GetParentIncidentInfoAsync(long incidentId)
    {
        var parentIncident = await _icmApiClientSDKService.GetParentIncidentInfoAsync(incidentId.ToString());
        return parentIncident is not null ? JsonSerializer.Serialize(parentIncident) : $"Cannot found parent incident for {incidentId}";
    }

    public async Task<string> AddParentIncidentLinkAsync(long incidentId, long parentIncidentId)
    {
        var res = await _icmApiClientSDKService.AddParentIncidentLinkAsync(incidentId.ToString(), parentIncidentId.ToString());
        if (!res.Success)
        {
            throw new Exception($"Failed to link parent incident {incidentId} with {parentIncidentId}. Error: {res.Message}");
        }
        return res.Message;
    }

    public async Task<string> RemoveParentIncidentLinkAsync(long incidentId)
    {
        var res = await _icmApiClientSDKService.RemoveParentIncidentLinkAsync(incidentId.ToString());
        if (!res.Success)
        {
            throw new Exception($"Failed to unlink parent incident for {incidentId}. Error: {res.Message}");
        }
        return res.Message;
    }
    #endregion

    private bool CheckIncidentState(Incident incident, IncidentStatus[] allowedStates)
    {
        if (Enum.TryParse<IncidentStatus>(incident.State, true, out var stateEnum))
        {
            return allowedStates.Contains(stateEnum);
        }
        else
        {
            throw new Exception($"Failed to parse incident state: {incident.State}");
        }
    }

    public async Task<List<string>> GetChildIncidentsInfoAsync(long incidentId)
    {
        var res = await _icmApiClientSDKService.GetChildIncidentsInfoAsync(incidentId.ToString());
        return res.Any() ?
                    res.Select(incident => JsonSerializer.Serialize(incident)).ToList()
                    : new List<string>() { $"Cannnot found any child incidents with {incidentId}" };
    }

    public async Task<List<ExternalLink>> GetIncidentRepairItemsAsync(long incidentId)
    {
        return await _icmApiClientSDKService.GetIncidentRepairItemsAsync(incidentId.ToString());
    }

    public async Task<List<Attachment>> ListIncidentAttachments(string incidentId)
    {
        return await _icmApiClientSDKService.ListIncidentAttachments(incidentId);
    }

    //public async Task<string> DownloadIncidentAttachment(string incidentId, string attachmentId)
    //{
    //    var res = await _icmApiClientSDKService.DownloadIncidentAttachment(incidentId, attachmentId);
    //    if (!res.Success)
    //    {
    //        throw new Exception($"Failed to download incident attachment. Error: {res.Message}");
    //    }
    //    return res.Message;
    //}
}

public class NullableICMAPIClient : IICMAPIClient
{
    public Task<string> AcknowledgeIncidentAsync(string incidentId, string acknowledgeContactAlias = "")
    {
        throw new NotImplementedException();
    }

    public Task<string> AddKeywordToIncident(string incidentId, string keyword)
    {
        throw new NotImplementedException();
    }

    public Task<string> AddParentIncidentLinkAsync(long incidentId, long parentIncidentId)
    {
        throw new NotImplementedException();
    }

    public Task<string> AddRelatedIncidentLinkAsync(long incidentId, long relatedIncidentId)
    {
        throw new NotImplementedException();
    }

    public Task<string> AddTagToIncident(string incidentId, string tag)
    {
        throw new NotImplementedException();
    }

    public Task<string> ChangeSeverityAsync(string incidentId, int severity, string discussionEntry, bool htmlRendering = true)
    {
        throw new NotImplementedException();
    }

    public Task<List<string>> GetChildIncidentsInfoAsync(long incidentId)
    {
        throw new NotImplementedException();
    }

    public Task<List<CustomField>> GetCustomFieldsAsync(string incidentId)
    {
        throw new NotImplementedException();
    }

    public Task<Incident> GetIncidentAsync(string incidentId)
    {
        throw new NotImplementedException();
    }

    public Task<List<Incident>> GetIncidentsAsync(uint limit, uint offset, DateTime? lastModifiedDate, string? owningServiceId, string? titleContains, string? owningTeamId = null, string? incidentType = null, string? createdBy = null, string? monitorId = null, string? severity = null, IEnumerable<string>? statuses = null)
    {
        throw new NotImplementedException();
    }

    public Task<List<string>> GetLinkedRelatedIncidentInfoAsync(long incidentId)
    {
        throw new NotImplementedException();
    }

    public Task<string> GetParentIncidentInfoAsync(long incidentId)
    {
        throw new NotImplementedException();
    }

    public bool IsEnabled()
    {
        throw new NotImplementedException();
    }

    public Task<string> MitigateIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string howFixed = "", string mitigateContactAlias = "")
    {
        throw new NotImplementedException();
    }

    public Task<string> PostDiscussionEntryAsync(string incidentId, string discussionEntry, bool htmlRendering = true)
    {
        throw new NotImplementedException();
    }

    public Task<string> RemoveParentIncidentLinkAsync(long incidentId)
    {
        throw new NotImplementedException();
    }

    public Task<string> RemoveRelatedIncidentLinkAsync(long incidentId, long relatedIncidentId)
    {
        throw new NotImplementedException();
    }

    public Task<string> ResolveIncidentAsync(string incidentId, string discussionEntry, bool isCustomerImpacting = false, bool isNoise = false, string resolveContactAlias = "")
    {
        throw new NotImplementedException();
    }

    public Task<string> SetIncidentTags(string incidentId, List<string> tags)
    {
        throw new NotImplementedException();
    }

    public Task<string> TransferIncidentAsync(string incidentId, string discussionEntry, string tenantId, string teamId)
    {
        throw new NotImplementedException();
    }

    public Task<string> AddIncidentAttachment(string incidentId, string fileName, string base64Content)
    {
        throw new NotImplementedException();
    }

    public Task<List<Attachment>> ListIncidentAttachments(string incidentId)
    {
        throw new NotImplementedException();
    }

    public Task<string> DownloadIncidentAttachment(string incidentId, string attachmentId)
    {
        throw new NotImplementedException();
    }

    public Task<List<Incident>> SearchIncidentsAsync(string searchString)
    {
        throw new NotImplementedException();
    }

    public Task<List<DescriptionEntry>> GetIncidentDiscussionEntriesAsync(string incidentId)
    {
        throw new NotImplementedException();
    }

    public Task<List<ExternalLink>> GetIncidentRepairItemsAsync(long incidentId)
    {
        throw new NotImplementedException();
    }
}

