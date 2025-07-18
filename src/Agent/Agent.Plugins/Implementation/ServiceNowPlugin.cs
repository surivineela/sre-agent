using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Agent.Core.Interfaces;
using Agent.Core.Models.ServiceNow;
using Agent.Logging;
using Agent.Plugins.Interface;
using Microsoft.Extensions.Logging;

namespace Agent.Plugins.Implementation;

public class ServiceNowPlugin : IServiceNowPlugin
{
    private readonly IServiceNowAPIClient _serviceNowApiClient;
    private readonly ILogger<ServiceNowPlugin> _logger;

    public ServiceNowPlugin(
        IServiceNowAPIClient serviceNowApiClient,
        ILogger<ServiceNowPlugin> logger)
    {
        _serviceNowApiClient = serviceNowApiClient ?? throw new ArgumentNullException(nameof(serviceNowApiClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ServiceNowIncident> GetServiceNowIncident(string incidentId)
    {
        var logMessage = $"[{nameof(ServiceNowPlugin)}_{nameof(GetServiceNowIncident)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        
        return await _serviceNowApiClient.GetIncidentAsync(incidentId);
    }

    public async Task<string> PostServiceNowDiscussionEntry(string incidentId, string discussionEntry)
    {
        var logMessage = $"[{nameof(ServiceNowPlugin)}_{nameof(PostServiceNowDiscussionEntry)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}.\n<b>discussionEntry</b>:\n {discussionEntry}";
        _logger.LogInternalInformation(logMessage);
        
        var result = await _serviceNowApiClient.PostDiscussionEntryAsync(incidentId, discussionEntry);
        return result;
    }

    public async Task<string> AcknowledgeServiceNowIncident(string incidentId)
    {
        var logMessage = $"[{nameof(ServiceNowPlugin)}_{nameof(AcknowledgeServiceNowIncident)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        
        return await _serviceNowApiClient.AcknowledgeIncidentAsync(incidentId);
    }

    public async Task<string> ResolveServiceNowIncident(string incidentId, string discussionEntry)
    {
        var logMessage = $"[{nameof(ServiceNowPlugin)}_{nameof(ResolveServiceNowIncident)}][{DateTime.UtcNow}] Invoked with incidentId {incidentId}";
        _logger.LogInternalInformation(logMessage);
        
        try
        {
            // First add the resolution note
            await PostServiceNowDiscussionEntry(incidentId, $"Resolution: {discussionEntry}");
            
            // Then resolve the incident
            var result = await _serviceNowApiClient.ResolveIncidentAsync(incidentId, discussionEntry);
            
            _logger.LogInternalInformation($"Successfully resolved ServiceNow incident {incidentId}");
            return result;
        }
        catch (Exception ex)
        {
            var errorMessage = $"Error resolving ServiceNow incident {incidentId}: {ex.Message}";
            _logger.LogInternalError(ex, errorMessage);
            throw new Exception(errorMessage, ex);
        }
    }
}
